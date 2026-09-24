namespace InsuranceClaims.Domain.RiskAssessment.Enums;

/// <summary>
/// Categories of fraud flags that can be raised during risk assessment.
/// </summary>
public enum FraudFlagType
{
    DuplicateClaim = 0,
    HighAmount = 1,
    InconsistentData = 2,
    SuspiciousPattern = 3,
    FrequentClaims = 4,
    DocumentTypeMismatch = 5,
    DocumentUnreadable = 6,
    DuplicateDocumentReused = 7,
    DocumentContentInconsistent = 8,
    DocumentVerificationFailed = 9
}
