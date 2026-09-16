namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Single source of truth for payout lifecycle state.
///
/// State machine:
///   Draft
///     ↓
///   PendingApproval
///     ├──→ Rejected         [terminal]
///     ├──→ RevisionRequested → Draft (after revision)
///     └──→ Approved
///            ↓
///         Processing
///           ├──→ Paid        [terminal]
///           └──→ Failed      [terminal]
/// </summary>
public enum PayoutStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    RevisionRequested = 4,
    Processing = 5,
    Paid = 6,
    Failed = 7
}
