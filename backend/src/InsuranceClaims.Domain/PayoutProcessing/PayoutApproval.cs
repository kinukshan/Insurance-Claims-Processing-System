using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Represents an approval decision on a payout.
///
/// ReviewerId and ReviewerName are derived from the authenticated server context
/// (JWT claims) — never from client-supplied request data.
/// Comments and Decision come from the reviewer's input.
/// </summary>
public class PayoutApproval : BaseEntity
{
    public Guid PayoutId { get; set; }

    /// <summary>
    /// The authenticated reviewer's user ID — set from server-side auth context.
    /// </summary>
    public Guid ReviewerId { get; set; }

    /// <summary>
    /// The authenticated reviewer's display name — set from server-side auth context.
    /// </summary>
    public string ReviewerName { get; set; } = string.Empty;

    public ApprovalDecisionType Decision { get; set; }

    public string Comments { get; set; } = string.Empty;

    public DateTime DecisionTimestamp { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    public virtual Payout? Payout { get; set; }
}
