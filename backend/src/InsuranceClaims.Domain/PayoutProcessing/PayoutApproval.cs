using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Represents an approval decision on a payout.
/// </summary>
public class PayoutApproval : BaseEntity
{
    public Guid PayoutId { get; set; }
    public Guid ReviewerId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
