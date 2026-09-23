using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
[Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPayoutRepository _payoutRepository;
    private readonly ILogger<PayoutsController> _logger;

    public PayoutsController(
        IPayoutService payoutService,
        IPaymentGateway paymentGateway,
        IPayoutRepository payoutRepository,
        ILogger<PayoutsController>? logger = null)
    {
        _payoutService = payoutService;
        _paymentGateway = paymentGateway;
        _payoutRepository = payoutRepository;
        _logger = logger ?? NullLogger<PayoutsController>.Instance;
    }

    /// <summary>
    /// Calculate and create a payout proposal for the given claim.
    /// All financial inputs retrieved from trusted backend sources.
    /// </summary>
    [HttpPost("calculate/{claimId:guid}")]
    public async Task<ActionResult<PayoutDto>> CalculatePayout(Guid claimId)
    {
        try
        {
            var result = await _payoutService.CalculatePayoutAsync(claimId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
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
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PayoutDto>> GetById(Guid id)
    {
        var payout = await _payoutService.GetByIdAsync(id);
        if (payout is null) return NotFound();
        return Ok(payout);
    }

    /// <summary>
    /// Get payout for a specific claim.
    /// </summary>
    [HttpGet("claim/{claimId:guid}")]
    public async Task<ActionResult<PayoutDto>> GetByClaimId(Guid claimId)
    {
        var payout = await _payoutService.GetByClaimIdAsync(claimId);
        if (payout is null) return NotFound();
        return Ok(payout);
    }

    /// <summary>
    /// Get paginated, filterable, sortable payout history.
    /// </summary>
    [HttpGet("history")]
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
    /// Update a draft payout (recalculate from current backend data).
    /// </summary>
    [HttpPut("{id:guid}")]
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
    /// Execute an approved payout. Must have valid human approval first.
    /// The AI must NEVER automatically execute a real payment.
    /// Restricted to Admin only — final payment execution is a high-impact action.
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PayoutDto>> ExecutePayout(Guid id)
    {
        try
        {
            // Transition to Processing
            var result = await _payoutService.ExecutePayoutAsync(id);

            // Call payment gateway (sandbox)
            var paymentResult = await _paymentGateway.ProcessPaymentAsync(
                id, result.FinalPayout);

            // Update payout with payment result
            var payout = await _payoutRepository.GetByIdAsync(id);
            if (payout is not null)
            {
                if (paymentResult.Success)
                {
                    payout.Status = PayoutStatus.Paid;
                    payout.PaymentReference = paymentResult.PaymentReference;
                }
                else
                {
                    payout.Status = PayoutStatus.Failed;
                }
                await _payoutRepository.UpdateAsync(payout);

                // Return updated DTO
                result = (await _payoutService.GetByIdAsync(id))!;
            }

            return Ok(result);
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
            _logger.LogError(ex, "Unexpected error during payout execution {PayoutId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred while executing the payout." });
        }
    }

    /// <summary>
    /// Delete a draft/invalid payout. Cannot delete approved/completed records.
    /// </summary>
    [HttpDelete("{id:guid}")]
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
    /// Follows the same pattern used by ClaimsController and PoliciesController.
    /// </summary>
    private (Guid ReviewerId, string ReviewerName) GetReviewerIdentity()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

        var userName = User.FindFirst(ClaimTypes.Name)?.Value
                    ?? User.FindFirst("name")?.Value
                    ?? "Unknown Reviewer";

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return (userId, userName);
        }

        // Fallback for dev/testing when JWT doesn't contain standard claims
        return (Guid.Empty, userName);
    }
}
