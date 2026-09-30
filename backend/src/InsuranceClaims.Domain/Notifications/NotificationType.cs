namespace InsuranceClaims.Domain.Notifications;

/// <summary>
/// Types of notifications the system can dispatch.
/// Each value maps to a distinct business event in the claims lifecycle.
/// </summary>
public enum NotificationType
{
    ClaimSubmitted = 0,
    ClaimApproved = 1,
    ClaimRejected = 2,
    ClaimWithdrawn = 3,
    DocumentVerificationComplete = 4,
    RiskAssessmentComplete = 5,
    PayoutApproved = 6,
    PayoutRejected = 7,
    PayoutCompleted = 8,
    AdditionalDocumentsRequired = 9,
    DocumentsVerified = 10,
    DocumentsNeedReview = 11,
    RiskAssessmentNeedsReview = 12,
    PayoutPendingApproval = 13,
    PayoutFailed = 14
}
