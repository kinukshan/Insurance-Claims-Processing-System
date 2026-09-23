using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.Services;

/// <summary>
/// EF Core implementation of IPayoutContextProvider.
///
/// Retrieves authoritative claim/policy data from the database.
/// Replaces StubPayoutContextProvider for real integrated operation.
///
/// Eligibility rule (based on actual domain lifecycle):
///   Eligible: Submitted, UnderReview, DocumentVerification,
///             AdditionalDocumentsRequired, RiskAssessment,
///             PendingApproval, Approved, PayoutProcessing
///   Blocked:  Draft, Withdrawn, Rejected, Closed
///
/// Rationale: Document Verification and Risk Assessment do NOT transition
/// ClaimStatus automatically. Claims remain Submitted throughout these
/// processes. Blocking Submitted would make payout impossible after the
/// full verification/risk pipeline completes.
/// </summary>
public class EfPayoutContextProvider : IPayoutContextProvider
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EfPayoutContextProvider> _logger;

    /// <summary>
    /// ClaimStatus values that are NOT eligible for payout.
    /// Draft: not yet submitted. Withdrawn: voluntarily cancelled.
    /// Rejected: denied. Closed: finalized.
    /// </summary>
    private static readonly ClaimStatus[] BlockedStatuses =
    {
        ClaimStatus.Draft,
        ClaimStatus.Withdrawn,
        ClaimStatus.Rejected,
        ClaimStatus.Closed
    };

    public EfPayoutContextProvider(
        ApplicationDbContext context,
        ILogger<EfPayoutContextProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PayoutContext?> GetPayoutContextAsync(Guid claimId)
    {
        // Retrieve the claim with its Policy (including PolicyType for display)
        var claim = await _context.Claims
            .Include(c => c.Policy)
                .ThenInclude(p => p!.PolicyType)
            .FirstOrDefaultAsync(c => c.Id == claimId);

        if (claim is null)
        {
            _logger.LogWarning("Payout context: Claim {ClaimId} not found.", claimId);
            return null;
        }

        // Check claim status eligibility
        if (BlockedStatuses.Contains(claim.Status))
        {
            _logger.LogWarning(
                "Payout context: Claim {ClaimId} has status '{Status}' which is not eligible for payout.",
                claimId, claim.Status);
            return null;
        }

        // Ensure the policy is loaded
        if (claim.Policy is null)
        {
            _logger.LogWarning(
                "Payout context: Claim {ClaimId} has no associated policy (PolicyId: {PolicyId}).",
                claimId, claim.PolicyId);
            return null;
        }

        return new PayoutContext
        {
            ClaimId = claim.Id,
            ApprovedClaimAmount = claim.ClaimedAmount,
            PolicyId = claim.PolicyId,
            PolicyType = claim.Policy.PolicyType?.Name ?? "Unknown",
            ClaimType = claim.ClaimType.ToString(),
            CoverageLimit = claim.Policy.CoverageLimit,
            Deductible = claim.Policy.Deductible
        };
    }
}
