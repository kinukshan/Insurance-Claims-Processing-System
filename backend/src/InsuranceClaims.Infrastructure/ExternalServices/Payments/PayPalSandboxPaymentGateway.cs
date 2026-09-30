using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AppGateway = InsuranceClaims.Application.PayoutProcessing.Interfaces;

namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// PayPal Sandbox REST API payment gateway adapter for Phase 1 payout integration.
///
/// Implements IPaymentGateway for asynchronous disbursement via the PayPal Payouts API.
///
/// IMPORTANT RULES:
/// - Real money is NEVER moved; runs strictly against the PayPal Sandbox environment.
/// - Live mode (PAYPAL_MODE=Live) is strictly prohibited and fails startup / calls safely.
/// - Credentials and tokens are managed server-side and never exposed to React or logged.
/// - Initial payout creation acceptance (201 Created) sets transaction/payout to Processing —
///   it does NOT mark the payout Paid.
/// - Only confirmed item-level success marks the payout Paid.
/// </summary>
public class PayPalSandboxPaymentGateway : AppGateway.IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly IPayPalAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalSandboxPaymentGateway> _logger;

    public PayPalSandboxPaymentGateway(
        HttpClient httpClient,
        IPayPalAuthService authService,
        IConfiguration configuration,
        ILogger<PayPalSandboxPaymentGateway>? logger = null)
    {
        _httpClient = httpClient;
        _authService = authService;
        _configuration = configuration;
        _logger = logger ?? NullLogger<PayPalSandboxPaymentGateway>.Instance;

        ValidateSandboxMode();
    }

    private void ValidateSandboxMode()
    {
        var mode = _configuration["PaymentGateway:PayPal:Mode"]
            ?? _configuration["PAYPAL_MODE"]
            ?? "Sandbox";

        if (string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Live PayPal mode is strictly prohibited. Only PayPal Sandbox is allowed.");
        }
    }

    /// <inheritdoc />
    public async Task<PaymentGatewayResult> CreatePayoutAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSandboxMode();

        var token = await _authService.GetAccessTokenAsync(cancellationToken);

        var senderBatchId = !string.IsNullOrWhiteSpace(request.SenderBatchId)
            ? request.SenderBatchId
            : $"insurance-payout-{request.PayoutId}";

        var senderItemId = !string.IsNullOrWhiteSpace(request.SenderItemId)
            ? request.SenderItemId
            : $"claim-{request.ClaimId}-payout-{request.PayoutId}";

        var recipientEmail = !string.IsNullOrWhiteSpace(request.RecipientEmail)
            ? request.RecipientEmail
            : $"policyholder-{request.ClaimId:N}[..8]@sandbox.example.com";

        var payload = new
        {
            sender_batch_header = new
            {
                sender_batch_id = senderBatchId,
                email_subject = "Insurance Claim Settlement",
                email_message = "Your approved insurance claim payout has been processed."
            },
            items = new[]
            {
                new
                {
                    recipient_type = "EMAIL",
                    amount = new
                    {
                        value = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                        currency = request.Currency
                    },
                    note = !string.IsNullOrWhiteSpace(request.Description)
                        ? request.Description
                        : $"Payout for claim {request.ClaimId}",
                    sender_item_id = senderItemId,
                    receiver = recipientEmail
                }
            }
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/payments/payouts")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("PayPal-Request-Id", request.IdempotencyKey);
        }

        _logger.LogInformation(
            "Sending PayPal sandbox payout request: PayoutId={PayoutId}, SenderBatchId={SenderBatchId}, Amount={Amount} {Currency}",
            request.PayoutId, senderBatchId, request.Amount, request.Currency);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            // 201 Created: Batch accepted for asynchronous processing
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            string? batchId = null;
            string? batchStatus = "PENDING";

            if (root.TryGetProperty("batch_header", out var header))
            {
                if (header.TryGetProperty("payout_batch_id", out var bId))
                    batchId = bId.GetString();
                if (header.TryGetProperty("batch_status", out var bStatus))
                    batchStatus = bStatus.GetString();
            }

            _logger.LogInformation(
                "PayPal sandbox payout batch accepted: BatchId={BatchId}, Status={Status}",
                batchId, batchStatus);

            // CRITICAL: 201 Created acceptance maps to ProviderStatus "processing" — NOT "succeeded"!
            return new PaymentGatewayResult
            {
                Success = true,
                Provider = "PayPalSandbox",
                ProviderTransactionId = batchId,
                ProviderBatchId = batchId,
                ProviderStatus = "processing",
                Message = "Payout batch accepted by PayPal sandbox for processing.",
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            // Error response
            string? errorName = "PAYPAL_ERROR";
            string? errorMessage = "PayPal sandbox rejected the payout request.";

            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n)) errorName = n.GetString();
                if (root.TryGetProperty("message", out var m)) errorMessage = m.GetString();
            }
            catch
            {
                // Fall back to HTTP status code description if not JSON
                errorMessage = $"PayPal returned HTTP {(int)response.StatusCode}";
            }

            _logger.LogWarning(
                "PayPal payout creation failed: StatusCode={StatusCode}, ErrorName={ErrorName}, Message={Message}",
                (int)response.StatusCode, errorName, errorMessage);

            return new PaymentGatewayResult
            {
                Success = false,
                Provider = "PayPalSandbox",
                ProviderStatus = "failed",
                FailureCode = errorName,
                Message = errorMessage,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    /// <inheritdoc />
    public async Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerTransactionId);
        ValidateSandboxMode();

        var token = await _authService.GetAccessTokenAsync(cancellationToken);

        // 1. Try querying as payout batch
        using var batchRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/payouts/{providerTransactionId}");
        batchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var batchResponse = await _httpClient.SendAsync(batchRequest, cancellationToken);
        if (batchResponse.IsSuccessStatusCode)
        {
            var content = await batchResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            string batchStatus = "PENDING";
            if (root.TryGetProperty("batch_header", out var header) &&
                header.TryGetProperty("batch_status", out var s))
            {
                batchStatus = s.GetString() ?? "PENDING";
            }

            // Inspect individual items if present
            if (root.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var firstItem = items[0];
                if (firstItem.TryGetProperty("transaction_status", out var itemStatusProp))
                {
                    var itemStatus = itemStatusProp.GetString() ?? batchStatus;
                    string? itemId = firstItem.TryGetProperty("payout_item_id", out var idProp) ? idProp.GetString() : null;
                    return new PaymentGatewayStatusResult
                    {
                        ProviderTransactionId = providerTransactionId,
                        ProviderBatchId = providerTransactionId,
                        ProviderItemId = itemId,
                        Status = MapPayPalStatusToNormalized(itemStatus),
                        RawStatus = itemStatus,
                        Success = true,
                        Message = $"PayPal payout item status: {itemStatus}",
                        UpdatedAt = DateTime.UtcNow
                    };
                }
            }

            return new PaymentGatewayStatusResult
            {
                ProviderTransactionId = providerTransactionId,
                ProviderBatchId = providerTransactionId,
                Status = MapPayPalStatusToNormalized(batchStatus),
                RawStatus = batchStatus,
                Success = true,
                Message = $"PayPal payout batch status: {batchStatus}",
                UpdatedAt = DateTime.UtcNow
            };
        }

        // 2. Try querying as payout item
        using var itemRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/payouts-item/{providerTransactionId}");
        itemRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var itemResponse = await _httpClient.SendAsync(itemRequest, cancellationToken);
        if (itemResponse.IsSuccessStatusCode)
        {
            var content = await itemResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            string itemStatus = "PENDING";
            if (root.TryGetProperty("transaction_status", out var s))
            {
                itemStatus = s.GetString() ?? "PENDING";
            }

            string? batchId = root.TryGetProperty("payout_batch_id", out var bIdProp) ? bIdProp.GetString() : null;
            return new PaymentGatewayStatusResult
            {
                ProviderTransactionId = providerTransactionId,
                ProviderBatchId = batchId,
                ProviderItemId = providerTransactionId,
                Status = MapPayPalStatusToNormalized(itemStatus),
                RawStatus = itemStatus,
                Success = true,
                Message = $"PayPal payout item status: {itemStatus}",
                UpdatedAt = DateTime.UtcNow
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateWebhookAsync(
        string payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ValidateSandboxMode();

        var webhookId = _configuration["PaymentGateway:PayPal:WebhookId"]
            ?? _configuration["PAYPAL_WEBHOOK_ID"];

        if (string.IsNullOrWhiteSpace(webhookId))
        {
            _logger.LogWarning("PayPal webhook validation failed: WebhookId is not configured.");
            return false;
        }

        // Case-insensitive header extraction
        string GetHeader(string key)
        {
            var match = headers.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));
            return match.Value ?? string.Empty;
        }

        var authAlgo = GetHeader("paypal-auth-algo");
        var certUrl = GetHeader("paypal-cert-url");
        var transmissionId = GetHeader("paypal-transmission-id");
        var transmissionSig = GetHeader("paypal-transmission-sig");
        var transmissionTime = GetHeader("paypal-transmission-time");

        if (string.IsNullOrWhiteSpace(authAlgo) ||
            string.IsNullOrWhiteSpace(certUrl) ||
            string.IsNullOrWhiteSpace(transmissionId) ||
            string.IsNullOrWhiteSpace(transmissionSig) ||
            string.IsNullOrWhiteSpace(transmissionTime))
        {
            _logger.LogWarning("PayPal webhook verification failed: Missing required PayPal verification headers.");
            return false;
        }

        // Security check: cert_url must point to a legitimate PayPal domain
        if (!Uri.TryCreate(certUrl, UriKind.Absolute, out var certUri) ||
            !certUri.Host.EndsWith("paypal.com", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("PayPal webhook verification failed: cert_url '{CertUrl}' is not from a paypal.com domain.", certUrl);
            return false;
        }

        JsonElement webhookEventElement;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            webhookEventElement = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PayPal webhook payload is not valid JSON.");
            return false;
        }

        var token = await _authService.GetAccessTokenAsync(cancellationToken);

        var verifyPayload = new
        {
            auth_algo = authAlgo,
            cert_url = certUrl,
            transmission_id = transmissionId,
            transmission_sig = transmissionSig,
            transmission_time = transmissionTime,
            webhook_id = webhookId,
            webhook_event = webhookEventElement
        };

        var verifyContent = JsonSerializer.Serialize(verifyPayload);
        using var verifyRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications/verify-webhook-signature")
        {
            Content = new StringContent(verifyContent, Encoding.UTF8, "application/json")
        };
        verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(verifyRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal signature verification endpoint returned HTTP {StatusCode}", (int)response.StatusCode);
            return false;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using var respDoc = JsonDocument.Parse(responseBody);
        if (respDoc.RootElement.TryGetProperty("verification_status", out var statusProp))
        {
            var status = statusProp.GetString();
            var isValid = string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
            if (!isValid)
            {
                _logger.LogWarning("PayPal signature verification returned status: {Status}", status);
            }
            return isValid;
        }

        return false;
    }

    /// <summary>
    /// Maps raw PayPal payout status string to normalized internal status string.
    /// </summary>
    public static string MapPayPalStatusToNormalized(string payPalStatus)
    {
        return payPalStatus.ToUpperInvariant() switch
        {
            "SUCCESS" => "succeeded",
            "PENDING" => "processing",
            "PROCESSING" => "processing",
            "UNCLAIMED" => "unclaimed",
            "RETURNED" => "returned",
            "BLOCKED" => "blocked",
            "ONHOLD" or "HELD" => "on_hold",
            "REFUNDED" => "refunded",
            "REVERSED" => "reversed",
            "FAILED" or "DENIED" or "CANCELED" => "failed",
            _ => "processing"
        };
    }
}
