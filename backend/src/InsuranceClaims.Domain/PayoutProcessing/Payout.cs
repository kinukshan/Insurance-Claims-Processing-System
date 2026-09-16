using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Represents a payout for an approved claim.
///
/// All authoritative financial values (ApprovedClaimAmount, CoverageLimit, Deductible)
/// are populated from trusted backend sources via IPayoutContextProvider —
/// never from client-supplied inputs.
/// </summary>
public class Payout : BaseEntity
{
    // ── Claim reference ──────────────────────────────────────────────
    public Guid ClaimId { get; set; }

    // ── Authoritative financial inputs (from trusted backend data) ──
    public decimal ApprovedClaimAmount { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal Deductible { get; set; }

    // ── Calculated outputs ───────────────────────────────────────────
    public decimal ProposedPayout { get; set; }
    public decimal FinalPayout { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────────
    public PayoutStatus Status { get; set; } = PayoutStatus.Draft;

    // ── Approval record (set on approval) ────────────────────────────
    /// <summary>
    /// The authenticated user ID who approved the payout.
    /// Derived from server-side JWT/auth context — never from the client.
    /// </summary>
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalTimestamp { get; set; }

    // ── Payment ──────────────────────────────────────────────────────
    public string? PaymentReference { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    // Navigation to Claim — FK configured in EF; actual Claim entity owned by Arulkumaran.
    public virtual ClaimsManagement.Claim? Claim { get; set; }

    /// <summary>Approval audit trail.</summary>
    public virtual ICollection<PayoutApproval> Approvals { get; set; } = new List<PayoutApproval>();

    // ── Domain Methods ───────────────────────────────────────────────

    /// <summary>
    /// Deterministic payout calculation.
    /// EligibleAmount = min(ApprovedClaimAmount, CoverageLimit)
    /// FinalPayout    = max(0, EligibleAmount - Deductible)
    /// </summary>
    public void CalculatePayout()
    {
        var eligible = Math.Min(ApprovedClaimAmount, CoverageLimit);
        ProposedPayout = Math.Max(0m, eligible - Deductible);
        FinalPayout = ProposedPayout; // May diverge after approval adjustments
    }

    /// <summary>
    /// Submit the draft payout for human approval.
    /// </summary>
    public void SubmitForApproval()
    {
        if (Status != PayoutStatus.Draft)
            throw new InvalidOperationException(
                $"Cannot submit payout for approval from status '{Status}'. Must be Draft.");

        Status = PayoutStatus.PendingApproval;
    }

    /// <summary>
    /// Whether this payout record may be deleted.
    /// Only Draft or RevisionRequested payouts can be deleted.
    /// Approved/Paid/Processing financial records are never hard-deleted.
    /// </summary>
    public bool CanBeDeleted()
    {
        return Status is PayoutStatus.Draft or PayoutStatus.RevisionRequested;
    }

    /// <summary>
    /// Validates whether a transition from the current status to the target status is legal.
    /// </summary>
    public static bool IsValidTransition(PayoutStatus from, PayoutStatus to)
    {
        return (from, to) switch
        {
            (PayoutStatus.Draft, PayoutStatus.PendingApproval) => true,
            (PayoutStatus.PendingApproval, PayoutStatus.Approved) => true,
            (PayoutStatus.PendingApproval, PayoutStatus.Rejected) => true,
            (PayoutStatus.PendingApproval, PayoutStatus.RevisionRequested) => true,
            (PayoutStatus.RevisionRequested, PayoutStatus.Draft) => true,
            (PayoutStatus.Approved, PayoutStatus.Processing) => true,
            (PayoutStatus.Processing, PayoutStatus.Paid) => true,
            (PayoutStatus.Processing, PayoutStatus.Failed) => true,
            _ => false
        };
    }
}
