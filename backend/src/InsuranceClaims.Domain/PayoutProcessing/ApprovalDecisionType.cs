namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// The decision a reviewer can make on a payout proposal.
/// </summary>
public enum ApprovalDecisionType
{
    Approved = 0,
    Rejected = 1,
    RevisionRequested = 2
}
