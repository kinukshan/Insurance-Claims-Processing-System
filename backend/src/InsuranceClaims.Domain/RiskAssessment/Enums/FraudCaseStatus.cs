namespace InsuranceClaims.Domain.RiskAssessment.Enums;

/// <summary>
/// Status of a fraud investigation case.
/// </summary>
public enum FraudCaseStatus
{
    Open = 0,
    UnderInvestigation = 1,
    Resolved = 2,
    Dismissed = 3
}
