using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a risk assessment for a claim.
/// </summary>
public class RiskAssessment : BaseEntity
{
    public Guid ClaimId { get; set; }
    public decimal RiskScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
