namespace InsuranceClaims.Domain.RiskAssessment.Enums;

/// <summary>
/// Recommendation outcome from a risk assessment.
/// </summary>
public enum RiskRecommendation
{
    /// <summary>Claim can proceed to the next workflow step.</summary>
    Proceed = 0,

    /// <summary>Claim should be escalated for manual review.</summary>
    Escalate = 1
}
