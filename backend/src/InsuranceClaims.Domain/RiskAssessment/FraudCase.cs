using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a fraud case for investigation.
/// </summary>
public class FraudCase : BaseEntity
{
    public Guid ClaimId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
