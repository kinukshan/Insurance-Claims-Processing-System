namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Status of an insurance claim.
/// </summary>
public enum ClaimStatus
{
    Submitted = 0,
    UnderReview = 1,
    DocumentVerification = 2,
    RiskAssessment = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    RevisionRequested = 7,
    PayoutProcessing = 8,
    Closed = 9
}
