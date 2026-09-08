using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a fraud flag raised during risk assessment.
/// </summary>
public class FraudFlag : BaseEntity
{
    public Guid RiskAssessmentId { get; set; }
    public string FlagType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
