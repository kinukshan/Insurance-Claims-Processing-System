namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Provider-independent payment transaction status.
///
/// This tracks the external payment provider lifecycle,
/// which is separate from PayoutStatus (internal domain lifecycle).
///
/// PayoutStatus tracks: Draft → PendingApproval → Approved → Processing → Paid/Failed
/// PaymentTransactionStatus tracks: Created → Processing → Succeeded → Failed → Cancelled
/// </summary>
public enum PaymentTransactionStatus
{
    Created = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Unclaimed = 5,
    Returned = 6,
    Blocked = 7,
    OnHold = 8,
    Reversed = 9,
    Refunded = 10
}
