using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Payout processing endpoints — Component D (Kinukshan).
///
/// Reviewer identity is derived from the authenticated server context.
///
/// Financial inputs (CoverageLimit, Deductible, ApprovedClaimAmount) come from
/// IPayoutContextProvider on the backend — never from client requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly INotificationOrchestrator? _notificationOrchestrator;
    private readonly IUserEmailResolver? _userEmailResolver;
    private readonly ILogger<PayoutsController> _logger;

    public PayoutsController(
        IPayoutService payoutService,
        IPaymentGateway paymentGateway,
        IPayoutRepository payoutRepository,
        IPaymentTransactionRepository transactionRepository,
        ILogger<PayoutsController>? logger = null,
        INotificationOrchestrator? notificationOrchestrator = null,
        IUserEmailResolver? userEmailResolver = null)
    {
        _payoutService = payoutService;
        _paymentGateway = paymentGateway;
        _payoutRepository = payoutRepository;
        _transactionRepository = transactionRepository;
        _logger = logger ?? NullLogger<PayoutsController>.Instance;
        _notificationOrchestrator = notificationOrchestrator;
        _userEmailResolver = userEmailResolver;
    }


    /// <summary>
    /// Calculate and create a payout proposal for the given claim.
    /// All financial inputs retrieved from trusted backend sources.
    /// Restricted to ClaimsAdjuster, Underwriter, and Admin.
    /// </summary>
    [HttpPost("calculate/{claimId:guid}")]
    [Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
    public async Task<ActionResult<PayoutDto>> CalculatePayout(Guid claimId)
    {
        try
        {
            var result = await _payoutService.CalculatePayoutAsync(claimId);

            // Await non-authoritative notification if pending approval
            if (_notificationOrchestrator != null && _userEmailResolver != null && result.Status == PayoutStatus.PendingApproval)
            {
                try
                {
                    var payout = await _payoutRepository.GetByIdAsync(result.Id);
                    if (payout?.Claim != null && payout.Claim.PolicyHolderId != Guid.Empty)
                    {
                        var email = await _userEmailResolver.GetEmailAsync(payout.Claim.PolicyHolderId);
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            var key = $"payout:{result.Id}:pending-approval";
                            await _notificationOrchestrator.NotifyAsync(
                                key,
                                payout.Claim.PolicyHolderId,
                                email,
                                result.ClaimId,
                                NotificationType.PayoutPendingApproval,
                                result.ClaimNumber,
                                result.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-authoritative notification failed for payout calculation {PayoutId}", result.Id);
                }
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (PolicyClaimCompatibilityException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message });
            }
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout calculation for claim {ClaimId}", claimId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while saving the payout calculation." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout calculation for claim {ClaimId}", claimId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while calculating the payout." });
        }
    }

    /// <summary>
    /// Get a payout by its ID.
    /// Accessible to staff (any claim) and Policyholders (own claims only).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayoutDto>> GetById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var payout = await _payoutService.GetByIdAsync(id, userId, role);
            if (payout is null) return NotFound();
            return Ok(payout);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Get payout for a specific claim.
    /// Accessible to staff (any claim) and Policyholders (own claims only).
    /// </summary>
    [HttpGet("claim/{claimId:guid}")]
    public async Task<ActionResult<PayoutDto>> GetByClaimId(Guid claimId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var payout = await _payoutService.GetByClaimIdAsync(claimId, userId, role);
            if (payout is null) return NotFound();
            return Ok(payout);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Get paginated, filterable, sortable payout history (Staff view).
    /// Restricted to ClaimsAdjuster, Underwriter, and Admin.
    /// </summary>
    [HttpGet("history")]
    [Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
    public async Task<ActionResult<PaginatedResult<PayoutDto>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PayoutStatus? status = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] bool sortDescending = true)
    {
        var query = new PayoutHistoryQueryDto
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            StatusFilter = status,
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await _payoutService.GetHistoryAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Get paginated payout history for the logged-in policyholder's own claims.
    /// User identity is derived from authenticated server context (JWT claims) — never from client request.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<PaginatedResult<PayoutDto>>> GetMyPayouts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PayoutStatus? status = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] bool sortDescending = true)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var query = new PayoutHistoryQueryDto
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            StatusFilter = status,
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await _payoutService.GetMyPayoutsAsync(userId, query);
        return Ok(result);
    }

    /// <summary>
    /// Update a draft payout (recalculate from current backend data).
    /// Restricted to ClaimsAdjuster, Underwriter, and Admin.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
    public async Task<ActionResult<PayoutDto>> UpdatePayout(Guid id)
    {
        try
        {
            var result = await _payoutService.UpdatePayoutAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Update transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout update {PayoutId}", id);
            return Conflict(new { error = "The payout was modified or processed by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout update {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while updating the payout." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout update {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while updating the payout." });
        }
    }

    /// <summary>
    /// Approve a payout.
    /// Reviewer identity derived from authenticated server context.
    /// Restricted to Underwriter and Admin roles (preparer/adjuster cannot approve their own work).
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Underwriter,Admin")]
    public async Task<ActionResult<PayoutDto>> ApprovePayout(
        Guid id, [FromBody] PayoutApprovalRequestDto request)
    {
        try
        {
            var (reviewerId, reviewerName) = GetReviewerIdentity();
            var result = await _payoutService.ApprovePayoutAsync(
                id, request.Comments, reviewerId, reviewerName);

            // Await non-authoritative notification for PayoutApproved (worded as Approved, not Paid)
            if (_notificationOrchestrator != null && _userEmailResolver != null)
            {
                try
                {
                    var payout = await _payoutRepository.GetByIdAsync(id);
                    if (payout?.Claim != null && payout.Claim.PolicyHolderId != Guid.Empty)
                    {
                        var email = await _userEmailResolver.GetEmailAsync(payout.Claim.PolicyHolderId);
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            var key = $"payout:{result.Id}:approved";
                            await _notificationOrchestrator.NotifyAsync(
                                key,
                                payout.Claim.PolicyHolderId,
                                email,
                                result.ClaimId,
                                NotificationType.PayoutApproved,
                                result.ClaimNumber,
                                result.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-authoritative notification failed for payout approval {PayoutId}", id);
                }
            }

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Approval transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout approval {PayoutId}", id);
            return Conflict(new { error = "The payout was modified or approved by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout approval {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while saving the payout approval." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout approval {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while processing the payout approval." });
        }
    }

    /// <summary>
    /// Reject a payout.
    /// Reviewer identity derived from authenticated server context.
    /// Restricted to Underwriter and Admin roles.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Underwriter,Admin")]
    public async Task<ActionResult<PayoutDto>> RejectPayout(
        Guid id, [FromBody] PayoutApprovalRequestDto request)
    {
        try
        {
            var (reviewerId, reviewerName) = GetReviewerIdentity();
            var result = await _payoutService.RejectPayoutAsync(
                id, request.Comments, reviewerId, reviewerName);

            // Await non-authoritative notification for PayoutRejected
            if (_notificationOrchestrator != null && _userEmailResolver != null)
            {
                try
                {
                    var payout = await _payoutRepository.GetByIdAsync(id);
                    if (payout?.Claim != null && payout.Claim.PolicyHolderId != Guid.Empty)
                    {
                        var email = await _userEmailResolver.GetEmailAsync(payout.Claim.PolicyHolderId);
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            var key = $"payout:{result.Id}:rejected";
                            await _notificationOrchestrator.NotifyAsync(
                                key,
                                payout.Claim.PolicyHolderId,
                                email,
                                result.ClaimId,
                                NotificationType.PayoutRejected,
                                result.ClaimNumber,
                                result.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-authoritative notification failed for payout rejection {PayoutId}", id);
                }
            }

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Reject transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout rejection {PayoutId}", id);
            return Conflict(new { error = "The payout was modified or approved by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout rejection {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while saving the payout rejection." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout rejection {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while processing the payout rejection." });
        }
    }

    /// <summary>
    /// Request revision on a payout.
    /// Reviewer identity derived from authenticated server context.
    /// Restricted to Underwriter and Admin roles.
    /// </summary>
    [HttpPost("{id:guid}/request-revision")]
    [Authorize(Roles = "Underwriter,Admin")]
    public async Task<ActionResult<PayoutDto>> RequestRevision(
        Guid id, [FromBody] PayoutApprovalRequestDto request)
    {
        try
        {
            var (reviewerId, reviewerName) = GetReviewerIdentity();
            var result = await _payoutService.RequestRevisionAsync(
                id, request.Comments, reviewerId, reviewerName);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Revision transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout revision request {PayoutId}", id);
            return Conflict(new { error = "The payout was modified or approved by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout revision request {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while saving the payout revision." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout revision request {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while processing the payout revision." });
        }
    }

    /// <summary>
    /// Execute an approved payout via the payment gateway.
    /// Must have valid human approval first.
    /// The AI must NEVER automatically execute a real payment.
    /// <summary>
    /// Get the active payment gateway provider information (e.g., "Mock", "PayPalSandbox").
    /// Used by frontend to render provider-aware execution states.
    /// </summary>
    [HttpGet("provider-info")]
    public ActionResult<object> GetPaymentProviderInfo()
    {
        var provider = _paymentGateway switch
        {
            Infrastructure.ExternalServices.Payments.MockPaymentGateway => "Mock",
            Infrastructure.ExternalServices.Payments.PayPalSandboxPaymentGateway => "PayPalSandbox",
            _ => _paymentGateway.GetType().Name.Contains("PayPal", StringComparison.OrdinalIgnoreCase)
                ? "PayPalSandbox"
                : "Mock"
        };

        return Ok(new
        {
            provider,
            isMock = string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase)
        });
    }

    /// <summary>
    /// Execute payout disbursement via configured payment gateway.
    /// Restricted to Admin only — final payment execution is a high-impact action.
    ///
    /// Execution order:
    /// 1. Authenticate + authorize Admin
    /// 2. Load payout, validate Approved status
    /// 3. Check idempotency / existing payment transaction
    /// 4. Transition payout to Processing
    /// 5. Create local PaymentTransaction as Created
    /// 6. Call IPaymentGateway
    /// 7. Persist provider result
    /// 8. Update payout status based on provider response
    /// 9. Return safe response
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PaymentExecutionResultDto>> ExecutePayout(Guid id)
    {
        try
        {
            // 1. Load payout (support lookup by payout ID or claim ID for robustness)
            var payout = await _payoutRepository.GetByIdAsync(id)
                ?? await _payoutRepository.GetByClaimIdAsync(id);
            if (payout is null)
            {
                return NotFound(new { error = $"Payout '{id}' not found." });
            }

            var payoutId = payout.Id;

            // 2. Validate payout amount
            if (payout.FinalPayout <= 0)
            {
                return BadRequest(new { error = "Payout amount must be greater than zero." });
            }

            // 3. Generate stable idempotency key and sender IDs using authoritative payoutId
            var idempotencyKey = $"payout:{payoutId}:execution";
            var senderBatchId = $"insurance-payout-{payoutId}";
            var senderItemId = $"claim-{payout.ClaimId}-payout-{payoutId}";

            // 4. Check idempotency — existing payment transaction for this key
            var existingTransaction = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey);
            if (existingTransaction is not null)
            {
                // Succeeded: do not retry
                if (existingTransaction.Status == PaymentTransactionStatus.Succeeded)
                {
                    _logger.LogInformation(
                        "Execution request for already-succeeded payout {PayoutId}, returning existing transaction {TransactionId}",
                        payoutId, existingTransaction.Id);

                    return Ok(new PaymentExecutionResultDto
                    {
                        PayoutId = payoutId,
                        TransactionId = existingTransaction.Id,
                        Provider = existingTransaction.Provider,
                        ProviderTransactionId = existingTransaction.ProviderTransactionId,
                        ProviderBatchId = existingTransaction.ProviderBatchId,
                        ProviderItemId = existingTransaction.ProviderItemId,
                        Status = existingTransaction.Status.ToString(),
                        Message = "Payment transaction already exists for this payout."
                    });
                }

                // Processing / in-flight: do not create new payout, return current status or query gateway
                if (existingTransaction.Status == PaymentTransactionStatus.Processing || existingTransaction.Status == PaymentTransactionStatus.Created)
                {
                    var lookupRef = existingTransaction.ProviderItemId ?? existingTransaction.ProviderBatchId ?? existingTransaction.SenderBatchId;
                    if (!string.IsNullOrEmpty(lookupRef))
                    {
                        try
                        {
                            var statusCheck = await _paymentGateway.GetPaymentStatusAsync(lookupRef);
                            if (statusCheck != null && statusCheck.Success && string.Equals(statusCheck.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                existingTransaction.Status = PaymentTransactionStatus.Succeeded;
                                existingTransaction.CompletedAt = DateTime.UtcNow;
                                if (!string.IsNullOrEmpty(statusCheck.ProviderItemId))
                                    existingTransaction.ProviderItemId = statusCheck.ProviderItemId;
                                await _transactionRepository.UpdateAsync(existingTransaction);

                                payout.Status = PayoutStatus.Paid;
                                payout.PaymentReference = existingTransaction.ProviderItemId ?? existingTransaction.ProviderTransactionId ?? existingTransaction.ProviderBatchId;
                                await _payoutRepository.UpdateAsync(payout);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to query status during duplicate check for payout {PayoutId}", payoutId);
                        }
                    }

                    return Ok(new PaymentExecutionResultDto
                    {
                        PayoutId = payoutId,
                        TransactionId = existingTransaction.Id,
                        Provider = existingTransaction.Provider,
                        ProviderTransactionId = existingTransaction.ProviderTransactionId,
                        ProviderBatchId = existingTransaction.ProviderBatchId,
                        ProviderItemId = existingTransaction.ProviderItemId,
                        Status = existingTransaction.Status.ToString(),
                        Message = existingTransaction.Status == PaymentTransactionStatus.Succeeded
                            ? "Payment confirmed succeeded via provider status check."
                            : "Payment transaction is already in progress for this payout."
                    });
                }

                // Failed: check if outcome was ambiguous (check gateway with existing sender_batch_id)
                if (existingTransaction.Status == PaymentTransactionStatus.Failed)
                {
                    var lookupRef = existingTransaction.ProviderItemId ?? existingTransaction.ProviderBatchId ?? existingTransaction.SenderBatchId;
                    if (!string.IsNullOrEmpty(lookupRef))
                    {
                        try
                        {
                            var statusCheck = await _paymentGateway.GetPaymentStatusAsync(lookupRef);
                            if (statusCheck != null && statusCheck.Success && string.Equals(statusCheck.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                existingTransaction.Status = PaymentTransactionStatus.Succeeded;
                                existingTransaction.CompletedAt = DateTime.UtcNow;
                                await _transactionRepository.UpdateAsync(existingTransaction);

                                payout.Status = PayoutStatus.Paid;
                                payout.PaymentReference = existingTransaction.ProviderItemId ?? existingTransaction.ProviderTransactionId ?? existingTransaction.ProviderBatchId;
                                await _payoutRepository.UpdateAsync(payout);

                                return Ok(new PaymentExecutionResultDto
                                {
                                    PayoutId = payoutId,
                                    TransactionId = existingTransaction.Id,
                                    Provider = existingTransaction.Provider,
                                    ProviderTransactionId = existingTransaction.ProviderTransactionId,
                                    ProviderBatchId = existingTransaction.ProviderBatchId,
                                    ProviderItemId = existingTransaction.ProviderItemId,
                                    Status = existingTransaction.Status.ToString(),
                                    Message = "Previous payment was already completed on provider."
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Status lookup on failed transaction failed for payout {PayoutId}", payoutId);
                        }
                    }
                }
            }

            // 5. Check for already-succeeded transaction
            if (await _transactionRepository.HasSucceededTransactionAsync(payoutId))
            {
                return Conflict(new { error = "This payout has already been successfully paid." });
            }

            // 6. Check for active (in-flight) payment transaction
            var activeTransaction = await _transactionRepository.GetActiveByPayoutIdAsync(payoutId);
            if (activeTransaction is not null)
            {
                return Conflict(new { error = "A payment transaction is already in progress for this payout." });
            }

            // 7. Resolve authoritative recipient email from backend user (safe fallback for mock or missing users)
            string? recipientEmail = null;
            try
            {
                if (payout.Claim != null && payout.Claim.PolicyHolderId != Guid.Empty)
                {
                    recipientEmail = await _payoutRepository.GetPolicyholderEmailAsync(payout.Claim.PolicyHolderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve policyholder email for payout {PayoutId}, using fallback", payoutId);
            }

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                recipientEmail = $"policyholder-{payout.Claim?.PolicyHolderId ?? payoutId}@insurance.internal";
            }

            // 8. Transition payout to Processing (validates Approved/Failed → Processing)
            if (payout.Status != PayoutStatus.Processing)
            {
                if (!Payout.IsValidTransition(payout.Status, PayoutStatus.Processing))
                {
                    return Conflict(new { error = $"Cannot execute payout from status '{payout.Status}'. Must be Approved." });
                }
                payout.Status = PayoutStatus.Processing;
                await _payoutRepository.UpdateAsync(payout);
            }

            // 9. Create or reuse local PaymentTransaction as Created
            PaymentTransaction transaction;
            if (existingTransaction is not null && existingTransaction.Status == PaymentTransactionStatus.Failed)
            {
                transaction = existingTransaction;
                transaction.Status = PaymentTransactionStatus.Created;
                transaction.FailureCode = null;
                transaction.FailureMessage = null;
                transaction.SenderBatchId = senderBatchId;
                transaction.SenderItemId = senderItemId;
                transaction.Recipient = recipientEmail;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
            }
            else
            {
                transaction = new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    PayoutId = payoutId,
                    ClaimId = payout.ClaimId,
                    Provider = "Pending",
                    IdempotencyKey = idempotencyKey,
                    SenderBatchId = senderBatchId,
                    SenderItemId = senderItemId,
                    Recipient = recipientEmail,
                    Amount = payout.FinalPayout,
                    Currency = "USD",
                    Status = PaymentTransactionStatus.Created
                };

                try
                {
                    await _transactionRepository.AddAsync(transaction);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _logger.LogWarning(ex, "Concurrent duplicate execution for payout {PayoutId}", payoutId);
                    var concurrent = await _transactionRepository.GetByIdempotencyKeyAsync(idempotencyKey);
                    return Ok(new PaymentExecutionResultDto
                    {
                        PayoutId = payoutId,
                        TransactionId = concurrent?.Id,
                        Provider = concurrent?.Provider,
                        ProviderTransactionId = concurrent?.ProviderTransactionId,
                        ProviderBatchId = concurrent?.ProviderBatchId,
                        ProviderItemId = concurrent?.ProviderItemId,
                        Status = concurrent?.Status.ToString() ?? "Unknown",
                        Message = "Payment transaction already exists (concurrent request)."
                    });
                }
            }

            // 10. Call IPaymentGateway
            PaymentGatewayResult gatewayResult;
            try
            {
                gatewayResult = await _paymentGateway.CreatePayoutAsync(new PaymentGatewayRequest
                {
                    PayoutId = payoutId,
                    ClaimId = payout.ClaimId,
                    Amount = payout.FinalPayout,
                    Currency = "USD",
                    BeneficiaryReference = $"CLAIM-{payout.ClaimId:N}"[..20],
                    IdempotencyKey = idempotencyKey,
                    RecipientEmail = recipientEmail,
                    SenderBatchId = senderBatchId,
                    SenderItemId = senderItemId,
                    Description = $"Payout for claim {payout.ClaimId}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment gateway exception for payout {PayoutId}", payoutId);
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureCode = "GATEWAY_EXCEPTION";
                transaction.FailureMessage = "Payment gateway returned an error.";
                transaction.CompletedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);

                payout = await _payoutRepository.GetByIdAsync(payoutId);
                if (payout is not null)
                {
                    payout.Status = PayoutStatus.Failed;
                    await _payoutRepository.UpdateAsync(payout);
                }

                return StatusCode(StatusCodes.Status502BadGateway,
                    new { error = "Payment gateway encountered an error. The payout was not completed." });
            }

            // 11. Persist provider result
            transaction.ProviderTransactionId = gatewayResult.ProviderTransactionId;
            transaction.ProviderBatchId = gatewayResult.ProviderBatchId;
            transaction.ProviderItemId = gatewayResult.ProviderItemId;
            transaction.Provider = gatewayResult.Provider;
            transaction.ProviderStatusRaw = gatewayResult.ProviderStatus;

            if (gatewayResult.Success)
            {
                // CRITICAL: PayPal Sandbox create request accepted -> Processing (NEVER Paid immediately)
                // Only mock gateway returning explicit "succeeded" can mark Succeeded right away.
                if (string.Equals(gatewayResult.ProviderStatus, "succeeded", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(gatewayResult.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
                {
                    transaction.Status = PaymentTransactionStatus.Succeeded;
                    transaction.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    // "processing" — awaiting asynchronous PayPal item-level webhook confirmation
                    transaction.Status = PaymentTransactionStatus.Processing;
                }
            }
            else
            {
                transaction.Status = PaymentTransactionStatus.Failed;
                transaction.FailureCode = gatewayResult.FailureCode;
                transaction.FailureMessage = gatewayResult.Message;
                transaction.CompletedAt = DateTime.UtcNow;
            }

            await _transactionRepository.UpdateAsync(transaction);

            // 12. Update payout status based on provider response
            payout = await _payoutRepository.GetByIdAsync(payoutId);
            if (payout is not null)
            {
                if (transaction.Status == PaymentTransactionStatus.Succeeded)
                {
                    payout.Status = PayoutStatus.Paid;
                    payout.PaymentReference = gatewayResult.ProviderTransactionId ?? gatewayResult.ProviderItemId ?? gatewayResult.ProviderBatchId;
                }
                else if (transaction.Status == PaymentTransactionStatus.Failed)
                {
                    payout.Status = PayoutStatus.Failed;
                }
                else
                {
                    // Stays in Processing awaiting webhook
                    payout.Status = PayoutStatus.Processing;
                    payout.PaymentReference = gatewayResult.ProviderBatchId;
                }

                await _payoutRepository.UpdateAsync(payout);

                // Await notification safely in active request scope — non-authoritative
                if (_notificationOrchestrator != null && _userEmailResolver != null && payout.Claim != null && payout.Claim.PolicyHolderId != Guid.Empty)
                {
                    try
                    {
                        var email = await _userEmailResolver.GetEmailAsync(payout.Claim.PolicyHolderId);
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            if (payout.Status == PayoutStatus.Paid)
                            {
                                var key = $"payout:{payoutId}:completed";
                                await _notificationOrchestrator.NotifyAsync(
                                    key,
                                    payout.Claim.PolicyHolderId,
                                    email,
                                    payout.ClaimId,
                                    NotificationType.PayoutCompleted,
                                    payout.Claim.ClaimNumber,
                                    payoutId);
                            }
                            else if (payout.Status == PayoutStatus.Failed)
                            {
                                var key = $"payout:{payoutId}:failed:{transaction.Id}";
                                await _notificationOrchestrator.NotifyAsync(
                                    key,
                                    payout.Claim.PolicyHolderId,
                                    email,
                                    payout.ClaimId,
                                    NotificationType.PayoutFailed,
                                    payout.Claim.ClaimNumber,
                                    payoutId);
                            }
                        }
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "Non-authoritative notification failed for payout execution {PayoutId}", payoutId);
                    }
                }
            }

            _logger.LogInformation(
                "Payment execution completed: PayoutId={PayoutId}, TransactionId={TransactionId}, Provider={Provider}, Status={Status}",
                payoutId, transaction.Id, transaction.Provider, transaction.Status);

            return Ok(new PaymentExecutionResultDto
            {
                PayoutId = payoutId,
                TransactionId = transaction.Id,
                Provider = transaction.Provider,
                ProviderTransactionId = transaction.ProviderTransactionId,
                ProviderBatchId = transaction.ProviderBatchId,
                ProviderItemId = transaction.ProviderItemId,
                Status = transaction.Status.ToString(),
                Message = gatewayResult.Message
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Execution transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout execution {PayoutId}", id);
            return Conflict(new { error = "The payout was modified or processed by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout execution {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while executing the payout." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout execution {PayoutId}: {Message}", id, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while executing the payout." });
        }
    }

    /// <summary>
    /// Refresh / sync payment status with the provider (e.g. PayPal Sandbox).
    /// Used when webhooks are delayed or Admin manually requests a status sync.
    /// Restricted to Admin.
    /// </summary>
    [HttpPost("{id:guid}/payments/sync")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PaymentExecutionResultDto>> SyncPaymentStatus(Guid id)
    {
        try
        {
            var payout = await _payoutRepository.GetByIdAsync(id);
            if (payout is null) return NotFound(new { error = $"Payout '{id}' not found." });

            var transaction = await _transactionRepository.GetActiveByPayoutIdAsync(id)
                ?? (await _transactionRepository.GetByPayoutIdAsync(id)).OrderByDescending(t => t.CreatedAt).FirstOrDefault();

            if (transaction is null)
            {
                return NotFound(new { error = "No payment transaction found for this payout." });
            }

            var lookupRef = transaction.ProviderItemId ?? transaction.ProviderBatchId ?? transaction.SenderBatchId;
            if (string.IsNullOrEmpty(lookupRef))
            {
                return BadRequest(new { error = "Transaction lacks provider reference for status lookup." });
            }

            var statusResult = await _paymentGateway.GetPaymentStatusAsync(lookupRef);
            if (statusResult != null && statusResult.Success)
            {
                var mappedStatus = statusResult.Status?.ToLowerInvariant() switch
                {
                    "succeeded" => PaymentTransactionStatus.Succeeded,
                    "processing" => PaymentTransactionStatus.Processing,
                    "failed" => PaymentTransactionStatus.Failed,
                    "unclaimed" => PaymentTransactionStatus.Unclaimed,
                    "returned" => PaymentTransactionStatus.Returned,
                    "blocked" => PaymentTransactionStatus.Blocked,
                    "onhold" => PaymentTransactionStatus.OnHold,
                    "reversed" => PaymentTransactionStatus.Reversed,
                    "refunded" => PaymentTransactionStatus.Refunded,
                    "cancelled" => PaymentTransactionStatus.Cancelled,
                    _ => transaction.Status
                };

                transaction.Status = mappedStatus;
                transaction.ProviderStatusRaw = statusResult.RawStatus ?? statusResult.Status;
                if (!string.IsNullOrEmpty(statusResult.ProviderItemId))
                    transaction.ProviderItemId = statusResult.ProviderItemId;

                if (mappedStatus == PaymentTransactionStatus.Succeeded)
                {
                    transaction.CompletedAt = DateTime.UtcNow;
                    payout.Status = PayoutStatus.Paid;
                    payout.PaymentReference = transaction.ProviderItemId ?? transaction.ProviderTransactionId ?? transaction.ProviderBatchId;
                    await _payoutRepository.UpdateAsync(payout);
                }
                else if (mappedStatus == PaymentTransactionStatus.Failed)
                {
                    transaction.CompletedAt = DateTime.UtcNow;
                    transaction.FailureCode = statusResult.FailureCode;
                    transaction.FailureMessage = statusResult.FailureMessage ?? statusResult.Message;
                    payout.Status = PayoutStatus.Failed;
                    await _payoutRepository.UpdateAsync(payout);
                }

                await _transactionRepository.UpdateAsync(transaction);
            }

            return Ok(new PaymentExecutionResultDto
            {
                PayoutId = id,
                TransactionId = transaction.Id,
                Provider = transaction.Provider,
                ProviderTransactionId = transaction.ProviderTransactionId,
                ProviderBatchId = transaction.ProviderBatchId,
                ProviderItemId = transaction.ProviderItemId,
                Status = transaction.Status.ToString(),
                Message = statusResult?.Message ?? "Payment status refreshed."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment sync for payout {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while syncing payment status." });
        }
    }

    /// <summary>
    /// Get payment transaction history for a payout.
    /// Staff see full details; Policyholders see safe status only.
    /// </summary>
    [HttpGet("{payoutId:guid}/payments")]
    public async Task<ActionResult<List<PaymentTransactionDto>>> GetPaymentTransactions(Guid payoutId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            // Verify payout access
            var payout = await _payoutService.GetByIdAsync(payoutId, userId, role);
            if (payout is null) return NotFound();

            var transactions = await _transactionRepository.GetByPayoutIdAsync(payoutId);

            var dtos = transactions.Select(t => new PaymentTransactionDto
            {
                Id = t.Id,
                PayoutId = t.PayoutId,
                Provider = t.Provider,
                ProviderTransactionId = t.ProviderTransactionId,
                ProviderBatchId = t.ProviderBatchId,
                ProviderItemId = t.ProviderItemId,
                SenderBatchId = t.SenderBatchId,
                SenderItemId = t.SenderItemId,
                Recipient = role == Role.Policyholder ? null : t.Recipient,
                ProviderStatusRaw = role == Role.Policyholder ? null : t.ProviderStatusRaw,
                Amount = t.Amount,
                Currency = t.Currency,
                Status = t.Status,
                FailureCode = role == Role.Policyholder ? null : t.FailureCode,
                FailureMessage = role == Role.Policyholder ? null : t.FailureMessage,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CompletedAt = t.CompletedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Delete a draft/invalid payout. Cannot delete approved/completed records.
    /// Restricted to ClaimsAdjuster, Underwriter, and Admin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
    public async Task<IActionResult> DeletePayout(Guid id)
    {
        try
        {
            await _payoutService.DeletePayoutAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Payout '{id}' not found." });
        }
        catch (InvalidOperationException ex) when (IsTransitionConflict(ex))
        {
            _logger.LogWarning(ex, "Delete transition conflict for payout {PayoutId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during payout deletion {PayoutId}", id);
            return Conflict(new { error = "The payout was modified by another user. Please refresh and try again." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database persistence error during payout deletion {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "A database error occurred while deleting the payout." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payout deletion {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while deleting the payout." });
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Extracts the current user's ID from JWT claims.
    /// Falls back to X-User-Id header in Development only.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("nameid")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
            return userId;

        // Dev fallback: allow X-User-Id header for testing without auth
        if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            Request.Headers.TryGetValue("X-User-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var headerUserId))
            return headerUserId;

        return Guid.Empty;
    }

    /// <summary>
    /// Extracts the current user's role from JWT claims.
    /// Falls back to X-User-Role header in Development only.
    /// Defaults to Policyholder if not found or cannot be parsed.
    /// </summary>
    private Role GetCurrentUserRole()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(roleClaim) && Enum.TryParse<Role>(roleClaim, ignoreCase: true, out var role))
            return role;

        // Dev fallback: allow X-User-Role header for testing without auth
        if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            Request.Headers.TryGetValue("X-User-Role", out var headerValue) &&
            Enum.TryParse<Role>(headerValue, ignoreCase: true, out var headerRole))
            return headerRole;

        return Role.Policyholder;
    }

    /// <summary>
    /// Check whether an InvalidOperationException represents a workflow state transition conflict.
    /// </summary>
    private static bool IsTransitionConflict(InvalidOperationException ex)
    {
        var msg = ex.Message;
        return msg.Contains("Cannot approve payout from status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot reject payout from status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot request revision from status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot execute payout from status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot update payout in status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot delete payout in status", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Must be PendingApproval", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Must be Approved", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Must be Draft", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract reviewer identity from the authenticated server context (JWT claims).
    /// Resolves reviewer ID from ClaimTypes.NameIdentifier / "sub" / "nameid".
    /// Resolves reviewer name in order:
    ///   1. Direct display name: ClaimTypes.Name / "name" / "unique_name"
    ///   2. Combined given name + surname: ClaimTypes.GivenName / "given_name" + ClaimTypes.Surname / "family_name"
    ///   3. Email fallback: ClaimTypes.Email / "email"
    ///   4. Identity name fallback: User.Identity.Name
    ///   5. Fallback: "Unknown Reviewer" only when no valid identity claims exist.
    /// </summary>
    private (Guid ReviewerId, string ReviewerName) GetReviewerIdentity()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("nameid")?.Value;

        var reviewerId = Guid.TryParse(userIdClaim, out var parsedId) ? parsedId : Guid.Empty;

        // 1. Direct name claims: ClaimTypes.Name, "name", "unique_name"
        var directName = User.FindFirst(ClaimTypes.Name)?.Value
                      ?? User.FindFirst("name")?.Value
                      ?? User.FindFirst("unique_name")?.Value;

        if (!string.IsNullOrWhiteSpace(directName))
        {
            return (reviewerId, directName.Trim());
        }

        // 2. Split name claims: GivenName + Surname / given_name + family_name
        var givenName = User.FindFirst(ClaimTypes.GivenName)?.Value
                     ?? User.FindFirst("given_name")?.Value;
        var surname = User.FindFirst(ClaimTypes.Surname)?.Value
                   ?? User.FindFirst("family_name")?.Value;

        var combinedName = $"{givenName} {surname}".Trim();
        if (!string.IsNullOrWhiteSpace(combinedName))
        {
            return (reviewerId, combinedName);
        }

        // 3. Fallback to Email claim
        var email = User.FindFirst(ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;

        if (!string.IsNullOrWhiteSpace(email))
        {
            return (reviewerId, email.Trim());
        }

        // 4. Fallback to User.Identity.Name if populated
        if (!string.IsNullOrWhiteSpace(User.Identity?.Name))
        {
            return (reviewerId, User.Identity.Name.Trim());
        }

        // 5. Ultimate fallback when no valid identity claims exist
        return (reviewerId, "Unknown Reviewer");
    }

    /// <summary>
    /// Check whether a DbUpdateException is caused by a unique constraint violation.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("23505", StringComparison.OrdinalIgnoreCase); // PostgreSQL unique violation
    }
}
