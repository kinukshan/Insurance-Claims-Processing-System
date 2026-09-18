using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Infrastructure.ExternalServices.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public PayoutsController(
        IPayoutService payoutService,
        IPaymentGateway paymentGateway,
        IPayoutRepository payoutRepository)
    {
        _payoutService = payoutService;
        _paymentGateway = paymentGateway;
        _payoutRepository = payoutRepository;
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
            return BadRequest(new { error = ex.Message });
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a payout.
    /// Reviewer identity derived from authenticated server context.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    // TODO: [Authorize(Roles = "Approver,Admin")]
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject a payout.
    /// Reviewer identity derived from authenticated server context.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    // TODO: [Authorize(Roles = "Approver,Admin")]
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request revision on a payout.
    /// Reviewer identity derived from authenticated server context.
    /// </summary>
    [HttpPost("{id:guid}/request-revision")]
    // TODO: [Authorize(Roles = "Approver,Admin")]
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Execute an approved payout. Must have valid human approval first.
    /// The AI must NEVER automatically execute a real payment.
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    // TODO: [Authorize(Roles = "Admin")]
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Extract reviewer identity from the authenticated server context.
    /// Currently returns placeholder values until shared auth middleware is wired.
    /// Will be replaced with:
    ///   User.FindFirst(ClaimTypes.NameIdentifier)
    ///   User.FindFirst(ClaimTypes.Name)
    /// </summary>
    private (Guid ReviewerId, string ReviewerName) GetReviewerIdentity()
    {
        // TODO: Replace with authenticated user context once auth middleware is available:
        //   var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "");
        //   var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        //   return (userId, userName);

        return (Guid.Parse("00000000-0000-0000-0000-000000000001"), "Staff Reviewer (Dev)");
    }
}
