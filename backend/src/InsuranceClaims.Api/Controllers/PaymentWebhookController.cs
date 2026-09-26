using System.Text.Json;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Webhook endpoints for payment provider status updates.
///
/// Supported endpoints:
/// - POST /api/payments/webhook/mock (Mock provider for testing)
/// - POST /api/payments/webhooks/paypal (PayPal Sandbox webhook)
/// - POST /api/payments/webhook/paypal (PayPal Sandbox alias)
///
/// Webhook processing is idempotent:
/// - Duplicate event IDs recorded in PaymentWebhookEvents are ignored
/// - Item-level success is authoritative for marking payouts Paid
/// - Batch-level success is informational and does not finalize payout
/// - Amount/currency is validated against authoritative internal transaction
/// - Unknown transactions are safely rejected
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IPayoutRepository _payoutRepository;
    private readonly INotificationOrchestrator? _notificationOrchestrator;
    private readonly IUserEmailResolver? _userEmailResolver;
    private readonly ILogger<PaymentWebhookController> _logger;

    public PaymentWebhookController(
        IPaymentGateway paymentGateway,
        IPaymentTransactionRepository transactionRepository,
        IPayoutRepository payoutRepository,
        ILogger<PaymentWebhookController>? logger = null,
        INotificationOrchestrator? notificationOrchestrator = null,
        IUserEmailResolver? userEmailResolver = null)
    {
        _paymentGateway = paymentGateway;
        _transactionRepository = transactionRepository;
        _payoutRepository = payoutRepository;
        _logger = logger ?? NullLogger<PaymentWebhookController>.Instance;
        _notificationOrchestrator = notificationOrchestrator;
        _userEmailResolver = userEmailResolver;
    }


    /// <summary>
    /// Receive payment status webhook from Mock provider.
    /// </summary>
    [HttpPost("webhook/mock")]
    public async Task<IActionResult> HandleMockWebhook([FromBody] PaymentWebhookEventDto webhookEvent)
    {
        // 1. Validate webhook authenticity
        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString());

        var isValid = await _paymentGateway.ValidateWebhookAsync(
            JsonSerializer.Serialize(webhookEvent), headers);

        if (!isValid)
        {
            _logger.LogWarning("Mock webhook validation failed");
            return Unauthorized(new { error = "Invalid webhook token." });
        }

        // 2. Find the payment transaction by provider transaction ID
        var transaction = await _transactionRepository.GetByProviderTransactionIdAsync(
            webhookEvent.ProviderTransactionId);

        if (transaction is null)
        {
            _logger.LogWarning(
                "Mock webhook received for unknown transaction: {ProviderTransactionId}",
                webhookEvent.ProviderTransactionId);
            return NotFound(new { error = "Unknown transaction." });
        }

        // 3. Replay safety: check for duplicate event ID in event ledger or transaction
        if (!string.IsNullOrEmpty(webhookEvent.EventId))
        {
            if (await _transactionRepository.HasWebhookEventBeenProcessedAsync("Mock", webhookEvent.EventId) ||
                (!string.IsNullOrEmpty(transaction.ProviderEventId) && transaction.ProviderEventId == webhookEvent.EventId))
            {
                _logger.LogInformation(
                    "Duplicate mock webhook event {EventId} for transaction {TransactionId}, ignoring",
                    webhookEvent.EventId, transaction.Id);
                return Ok(new { status = "already_processed" });
            }
        }

        // 4. Amount verification (if provided)
        if (webhookEvent.Amount.HasValue &&
            webhookEvent.Amount.Value != transaction.Amount)
        {
            _logger.LogWarning(
                "Mock webhook amount mismatch: expected {Expected}, received {Received} for transaction {TransactionId}",
                transaction.Amount, webhookEvent.Amount.Value, transaction.Id);
            return BadRequest(new { error = "Amount mismatch." });
        }

        // 5. Check current transaction state — don't allow regressive transitions
        if (transaction.Status == PaymentTransactionStatus.Succeeded)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} already succeeded, ignoring mock webhook",
                transaction.Id);
            return Ok(new { status = "already_succeeded" });
        }

        if (transaction.Status == PaymentTransactionStatus.Cancelled)
        {
            _logger.LogInformation(
                "Transaction {TransactionId} already cancelled, ignoring mock webhook",
                transaction.Id);
            return Ok(new { status = "already_cancelled" });
        }

        // 6. Process the webhook event
        var previousStatus = transaction.Status;
        transaction.ProviderEventId = webhookEvent.EventId;

        switch (webhookEvent.EventType.ToLowerInvariant())
        {
            case "payment.processing":
                transaction.Status = PaymentTransactionStatus.Processing;
                break;

            case "payment.succeeded":
                transaction.Status = PaymentTransactionStatus.Succeeded;
                transaction.CompletedAt = DateTime.UtcNow;
                break;

            case "payment.failed":
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureCode = webhookEvent.FailureCode;
                transaction.FailureMessage = webhookEvent.FailureMessage;
                transaction.CompletedAt = DateTime.UtcNow;
                break;

            default:
                _logger.LogWarning("Unknown mock webhook event type: {EventType}", webhookEvent.EventType);
                return BadRequest(new { error = $"Unknown event type: {webhookEvent.EventType}" });
        }

        await _transactionRepository.UpdateAsync(transaction);

        // Record in PaymentWebhookEvents
        if (!string.IsNullOrEmpty(webhookEvent.EventId))
        {
            await _transactionRepository.AddWebhookEventAsync(new PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),
                Provider = "Mock",
                ProviderEventId = webhookEvent.EventId,
                PaymentTransactionId = transaction.Id,
                EventType = webhookEvent.EventType,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                ProcessingStatus = "Processed"
            });
        }

        // 7. Update payout status when appropriate
        var payout = await _payoutRepository.GetByIdAsync(transaction.PayoutId);
        if (payout is not null)
        {
            if (transaction.Status == PaymentTransactionStatus.Succeeded)
            {
                payout.Status = PayoutStatus.Paid;
                payout.PaymentReference = transaction.ProviderTransactionId;
                await _payoutRepository.UpdateAsync(payout);
            }
            else if (transaction.Status == PaymentTransactionStatus.Failed)
            {
                payout.Status = PayoutStatus.Failed;
                await _payoutRepository.UpdateAsync(payout);
            }

            await TryNotifyPayoutStatusAsync(payout, transaction);
        }


        _logger.LogInformation(
            "Mock webhook processed: TransactionId={TransactionId}, Event={EventType}, " +
            "PreviousStatus={PreviousStatus}, NewStatus={NewStatus}",
            transaction.Id, webhookEvent.EventType, previousStatus, transaction.Status);

        return Ok(new { status = "processed" });
    }

    /// <summary>
    /// Receive PayPal Sandbox webhook notifications.
    /// Handles both batch-level and item-level events.
    /// Validates HMAC signature, cert URL, auth algorithm, and replay attacks.
    /// Item-level success is authoritative for completing payouts.
    /// </summary>
    [HttpPost("webhooks/paypal")]
    [HttpPost("webhook/paypal")]
    public async Task<IActionResult> HandlePayPalWebhook()
    {
        // 1. Extract required PayPal webhook headers
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        string GetHeader(string key)
        {
            var match = headers.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));
            return match.Value ?? string.Empty;
        }

        var transmissionId = GetHeader("paypal-transmission-id");
        var transmissionTime = GetHeader("paypal-transmission-time");
        var transmissionSig = GetHeader("paypal-transmission-sig");
        var certUrl = GetHeader("paypal-cert-url");
        var authAlgo = GetHeader("paypal-auth-algo");

        if (string.IsNullOrWhiteSpace(transmissionId) ||
            string.IsNullOrWhiteSpace(transmissionTime) ||
            string.IsNullOrWhiteSpace(transmissionSig) ||
            string.IsNullOrWhiteSpace(certUrl) ||
            string.IsNullOrWhiteSpace(authAlgo))
        {
            _logger.LogWarning("PayPal webhook rejected: missing required transmission headers.");
            return BadRequest(new { error = "Missing required PayPal webhook headers." });
        }

        // 2. Read raw request body for exact cryptographic signature verification
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { error = "Empty webhook payload." });
        }

        // 3. Cryptographically verify webhook authenticity via PayPal API
        var isValid = await _paymentGateway.ValidateWebhookAsync(rawBody, headers);
        if (!isValid)
        {
            _logger.LogWarning("PayPal webhook verification rejected: signature or authenticity validation failed.");
            return Unauthorized(new { error = "Invalid PayPal webhook signature." });
        }

        // 4. Parse payload
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PayPal webhook payload is not valid JSON.");
            return BadRequest(new { error = "Invalid JSON payload." });
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
            var eventType = root.TryGetProperty("event_type", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
            {
                return BadRequest(new { error = "Missing event id or event_type." });
            }

            // 5. Replay protection: check dedicated PaymentWebhookEvent ledger
            if (await _transactionRepository.HasWebhookEventBeenProcessedAsync("PayPal", eventId))
            {
                _logger.LogInformation("PayPal webhook event {EventId} already processed, ignoring duplicate.", eventId);
                return Ok(new { status = "already_processed" });
            }

            if (!root.TryGetProperty("resource", out var resource))
            {
                return BadRequest(new { error = "Missing webhook resource." });
            }

            // 6. Extract identifiers from resource
            string? payoutBatchId = null;
            string? payoutItemId = null;
            string? transactionId = null;
            string? senderItemId = null;
            string? senderBatchId = null;

            if (resource.TryGetProperty("payout_batch_id", out var pbProp))
                payoutBatchId = pbProp.GetString();

            if (resource.TryGetProperty("payout_item_id", out var piProp))
                payoutItemId = piProp.GetString();

            if (resource.TryGetProperty("transaction_id", out var tiProp))
                transactionId = tiProp.GetString();

            if (resource.TryGetProperty("payout_item", out var pItemObj))
            {
                if (pItemObj.TryGetProperty("sender_item_id", out var siProp))
                    senderItemId = siProp.GetString();
            }

            if (resource.TryGetProperty("batch_header", out var bhObj))
            {
                if (bhObj.TryGetProperty("payout_batch_id", out var bhPbProp))
                    payoutBatchId = bhPbProp.GetString() ?? payoutBatchId;

                if (bhObj.TryGetProperty("sender_batch_header", out var sbhObj) &&
                    sbhObj.TryGetProperty("sender_batch_id", out var sbProp))
                {
                    senderBatchId = sbProp.GetString();
                }
            }

            // 7. Match against internal PaymentTransaction
            PaymentTransaction? transaction = null;
            if (!string.IsNullOrEmpty(payoutItemId))
                transaction = await _transactionRepository.GetByProviderItemIdAsync(payoutItemId);

            if (transaction == null && !string.IsNullOrEmpty(senderItemId))
                transaction = await _transactionRepository.GetBySenderItemIdAsync(senderItemId);

            if (transaction == null && !string.IsNullOrEmpty(payoutBatchId))
                transaction = await _transactionRepository.GetByProviderBatchIdAsync(payoutBatchId);

            if (transaction == null && !string.IsNullOrEmpty(senderBatchId))
                transaction = await _transactionRepository.GetBySenderBatchIdAsync(senderBatchId);

            if (transaction == null && !string.IsNullOrEmpty(transactionId))
                transaction = await _transactionRepository.GetByProviderTransactionIdAsync(transactionId);

            if (transaction is null)
            {
                _logger.LogWarning(
                    "PayPal webhook {EventId} ({EventType}) received for unknown transaction. BatchId={BatchId}, ItemId={ItemId}, SenderItem={SenderItem}",
                    eventId, eventType, payoutBatchId, payoutItemId, senderItemId);

                // Store in ledger as unmatched for replay safety and audit
                await _transactionRepository.AddWebhookEventAsync(new PaymentWebhookEvent
                {
                    Id = Guid.NewGuid(),
                    Provider = "PayPal",
                    ProviderEventId = eventId,
                    PaymentTransactionId = null,
                    EventType = eventType,
                    ReceivedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessingStatus = "Unmatched"
                });

                return NotFound(new { error = "Transaction not found." });
            }

            // 8. Amount and Currency validation before finalizing
            if (string.Equals(eventType, "PAYMENT.PAYOUTS-ITEM.SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                if (resource.TryGetProperty("payout_item", out var itemEl) &&
                    itemEl.TryGetProperty("amount", out var amtEl))
                {
                    var valStr = amtEl.TryGetProperty("value", out var v) ? v.GetString() : null;
                    var currStr = amtEl.TryGetProperty("currency", out var c) ? c.GetString() : null;

                    if (decimal.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var webhookAmount))
                    {
                        if (webhookAmount != transaction.Amount ||
                            (!string.IsNullOrEmpty(currStr) && !string.Equals(currStr, transaction.Currency, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning(
                                "PayPal webhook amount/currency mismatch: Transaction {TransactionId} expected {ExpectedAmt} {ExpectedCurr}, received {ReceivedAmt} {ReceivedCurr}",
                                transaction.Id, transaction.Amount, transaction.Currency, webhookAmount, currStr);

                            await _transactionRepository.AddWebhookEventAsync(new PaymentWebhookEvent
                            {
                                Id = Guid.NewGuid(),
                                Provider = "PayPal",
                                ProviderEventId = eventId,
                                PaymentTransactionId = transaction.Id,
                                EventType = eventType,
                                ReceivedAt = DateTime.UtcNow,
                                ProcessedAt = DateTime.UtcNow,
                                ProcessingStatus = "AmountMismatch"
                            });

                            return BadRequest(new { error = "Amount or currency mismatch." });
                        }
                    }
                }
            }

            // 9. Process events: batch-level vs item-level
            var previousStatus = transaction.Status;

            switch (eventType.ToUpperInvariant())
            {
                // BATCH LEVEL: Informational only. Batch success does NOT automatically mean item success.
                case "PAYMENT.PAYOUTSBATCH.PROCESSING":
                    _logger.LogInformation("PayPal batch processing notification for transaction {TransactionId}", transaction.Id);
                    break;

                case "PAYMENT.PAYOUTSBATCH.SUCCESS":
                    _logger.LogInformation("PayPal batch success notification for transaction {TransactionId}. Awaiting item success.", transaction.Id);
                    break;

                case "PAYMENT.PAYOUTSBATCH.DENIED":
                    transaction.Status = PaymentTransactionStatus.Failed;
                    transaction.FailureCode = "BATCH_DENIED";
                    transaction.FailureMessage = "PayPal payout batch was denied.";
                    transaction.CompletedAt = DateTime.UtcNow;
                    break;

                // ITEM LEVEL: Authoritative transitions
                case "PAYMENT.PAYOUTS-ITEM.SUCCEEDED":
                    if (transaction.Status != PaymentTransactionStatus.Succeeded)
                    {
                        transaction.Status = PaymentTransactionStatus.Succeeded;
                        transaction.CompletedAt = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(transactionId))
                            transaction.ProviderTransactionId = transactionId;
                        if (!string.IsNullOrEmpty(payoutItemId))
                            transaction.ProviderItemId = payoutItemId;
                    }
                    break;

                case "PAYMENT.PAYOUTS-ITEM.FAILED":
                    transaction.Status = PaymentTransactionStatus.Failed;
                    transaction.FailureCode = ExtractErrorName(resource) ?? "ITEM_FAILED";
                    transaction.FailureMessage = ExtractErrorMessage(resource) ?? "PayPal payout item failed.";
                    transaction.CompletedAt = DateTime.UtcNow;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.CANCELED":
                    transaction.Status = PaymentTransactionStatus.Cancelled;
                    transaction.FailureCode = "ITEM_CANCELED";
                    transaction.FailureMessage = "PayPal payout item was canceled.";
                    transaction.CompletedAt = DateTime.UtcNow;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.DENIED":
                    transaction.Status = PaymentTransactionStatus.Failed;
                    transaction.FailureCode = "ITEM_DENIED";
                    transaction.FailureMessage = "PayPal payout item was denied.";
                    transaction.CompletedAt = DateTime.UtcNow;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.BLOCKED":
                    transaction.Status = PaymentTransactionStatus.Blocked;
                    transaction.FailureCode = "ITEM_BLOCKED";
                    transaction.FailureMessage = "PayPal payout item was blocked.";
                    break;

                case "PAYMENT.PAYOUTS-ITEM.HELD":
                    transaction.Status = PaymentTransactionStatus.OnHold;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.UNCLAIMED":
                    transaction.Status = PaymentTransactionStatus.Unclaimed;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.RETURNED":
                    transaction.Status = PaymentTransactionStatus.Returned;
                    break;

                case "PAYMENT.PAYOUTS-ITEM.REFUNDED":
                    transaction.Status = PaymentTransactionStatus.Refunded;
                    break;

                default:
                    _logger.LogInformation("Unhandled PayPal webhook event type {EventType} for transaction {TransactionId}", eventType, transaction.Id);
                    break;
            }

            // Update transaction
            transaction.ProviderEventId = eventId;
            transaction.ProviderStatusRaw = eventType;
            await _transactionRepository.UpdateAsync(transaction);

            // 10. Record event in PaymentWebhookEvents ledger
            await _transactionRepository.AddWebhookEventAsync(new PaymentWebhookEvent
            {
                Id = Guid.NewGuid(),
                Provider = "PayPal",
                ProviderEventId = eventId,
                PaymentTransactionId = transaction.Id,
                EventType = eventType,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                ProcessingStatus = "Processed"
            });

            // 11. Update Payout aggregate
            var payout = await _payoutRepository.GetByIdAsync(transaction.PayoutId);
            if (payout is not null)
            {
                if (transaction.Status == PaymentTransactionStatus.Succeeded)
                {
                    payout.Status = PayoutStatus.Paid;
                    payout.PaymentReference = transaction.ProviderItemId ?? transaction.ProviderTransactionId ?? transaction.ProviderBatchId;
                    await _payoutRepository.UpdateAsync(payout);
                }
                else if (transaction.Status == PaymentTransactionStatus.Failed)
                {
                    payout.Status = PayoutStatus.Failed;
                    await _payoutRepository.UpdateAsync(payout);
                }
                // Unclaimed, OnHold, Blocked, Processing: Payout remains in Processing (NOT Paid, NOT Failed)

                await TryNotifyPayoutStatusAsync(payout, transaction);
            }

            _logger.LogInformation(
                "PayPal webhook processed: Event={EventType}, TransactionId={TransactionId}, PayoutId={PayoutId}, Status={Status}",
                eventType, transaction.Id, transaction.PayoutId, transaction.Status);

            return Ok(new { status = "processed", eventType });
        }
    }

    private async Task TryNotifyPayoutStatusAsync(Payout payout, PaymentTransaction transaction)
    {
        if (_notificationOrchestrator == null || _userEmailResolver == null) return;
        if (payout.Claim == null || payout.Claim.PolicyHolderId == Guid.Empty) return;

        try
        {
            var email = await _userEmailResolver.GetEmailAsync(payout.Claim.PolicyHolderId);
            if (string.IsNullOrWhiteSpace(email)) return;

            if (payout.Status == PayoutStatus.Paid)
            {
                var key = $"payout:{payout.Id}:completed";
                await _notificationOrchestrator.NotifyAsync(
                    key,
                    payout.Claim.PolicyHolderId,
                    email,
                    payout.ClaimId,
                    NotificationType.PayoutCompleted,
                    payout.Claim.ClaimNumber,
                    payout.Id);
            }
            else if (payout.Status == PayoutStatus.Failed)
            {
                var key = $"payout:{payout.Id}:failed:{transaction.Id}";
                await _notificationOrchestrator.NotifyAsync(
                    key,
                    payout.Claim.PolicyHolderId,
                    email,
                    payout.ClaimId,
                    NotificationType.PayoutFailed,
                    payout.Claim.ClaimNumber,
                    payout.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Non-authoritative notification failed for webhook payout {PayoutId}", payout.Id);
        }
    }

    private static string? ExtractErrorName(JsonElement resource)
    {
        if (resource.TryGetProperty("errors", out var errors))
        {
            if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                if (first.TryGetProperty("name", out var n)) return n.GetString();
            }
            else if (errors.ValueKind == JsonValueKind.Object)
            {
                if (errors.TryGetProperty("name", out var n)) return n.GetString();
            }
        }
        return null;
    }

    private static string? ExtractErrorMessage(JsonElement resource)
    {
        if (resource.TryGetProperty("errors", out var errors))
        {
            if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                if (first.TryGetProperty("message", out var m)) return m.GetString();
            }
            else if (errors.ValueKind == JsonValueKind.Object)
            {
                if (errors.TryGetProperty("message", out var m)) return m.GetString();
            }
        }
        return null;
    }
}
