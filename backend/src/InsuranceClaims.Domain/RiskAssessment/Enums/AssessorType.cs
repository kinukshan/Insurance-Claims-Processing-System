namespace InsuranceClaims.Domain.RiskAssessment.Enums;

/// <summary>
/// Identifies the source that produced a risk assessment.
/// </summary>
public enum AssessorType
{
    /// <summary>Deterministic backend rules.</summary>
    System = 0,

    /// <summary>Agentic AI fraud/risk agent.</summary>
    AI = 1,

    /// <summary>Human reviewer override.</summary>
    Manual = 2
}
