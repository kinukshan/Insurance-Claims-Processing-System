namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Status of a payout.
/// </summary>
public enum PayoutStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    RevisionRequested = 4,
    Processed = 5,
    Failed = 6
}
