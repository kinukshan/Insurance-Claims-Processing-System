namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Status of an insurance claim throughout its lifecycle.
/// </summary>
public enum ClaimStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    DocumentVerification = 3,
    AdditionalDocumentsRequired = 4,
    RiskAssessment = 5,
    PendingApproval = 6,
    Approved = 7,
    Rejected = 8,
    Withdrawn = 9,
    PayoutProcessing = 10,
    Closed = 11
}
