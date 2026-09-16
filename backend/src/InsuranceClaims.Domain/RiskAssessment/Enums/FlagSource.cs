namespace InsuranceClaims.Domain.RiskAssessment.Enums;

/// <summary>
/// Source that raised a fraud flag.
/// </summary>
public enum FlagSource
{
    /// <summary>Raised by deterministic backend rules.</summary>
    Rule = 0,

    /// <summary>Raised by the agentic AI fraud/risk agent.</summary>
    AI = 1
}
