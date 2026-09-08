using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Represents a payout for an approved claim.
/// </summary>
public class Payout : BaseEntity
{
    public Guid ClaimId { get; set; }
    public decimal Amount { get; set; }
    public decimal DeductibleApplied { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Draft;

    // TODO: Add properties during implementation
}
