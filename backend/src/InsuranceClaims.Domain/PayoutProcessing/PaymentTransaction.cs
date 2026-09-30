using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Represents an external payment transaction created via a payment provider.
///
/// Each PaymentTransaction belongs to exactly one Payout.
/// A Payout may have multiple PaymentTransactions (e.g., failed attempt + retry),
/// but only one can succeed.
///
/// IdempotencyKey is unique and prevents duplicate payment creation.
/// ProviderEventId prevents duplicate webhook processing.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    /// <summary>FK to the parent Payout.</summary>
    public Guid PayoutId { get; set; }

    /// <summary>Associated claim identifier (denormalized for operational queries).</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Payment provider name (e.g., "Mock", "Stripe").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Transaction identifier from the payment provider.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>
    /// Stable idempotency key. Must be unique per payment attempt.
    /// Format: "payout:{PayoutId}:execution:{attempt}" or similar.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Payment amount (from the authoritative approved payout).</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Payment transaction lifecycle status.</summary>
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Created;

    /// <summary>Provider failure code, if any.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Provider failure message, if any.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Provider batch identifier (e.g. PayPal payout_batch_id).</summary>
    public string? ProviderBatchId { get; set; }

    /// <summary>Provider item identifier (e.g. PayPal payout_item_id).</summary>
    public string? ProviderItemId { get; set; }

    /// <summary>Stable sender batch identifier (e.g. insurance-payout-{PayoutId}).</summary>
    public string? SenderBatchId { get; set; }

    /// <summary>Stable sender item identifier (e.g. claim-{ClaimId}-payout-{PayoutId}).</summary>
    public string? SenderItemId { get; set; }

    /// <summary>Trusted recipient reference (e.g. policyholder email).</summary>
    public string? Recipient { get; set; }

    /// <summary>Raw provider status string (e.g. "PENDING", "SUCCESS", "UNCLAIMED").</summary>
    public string? ProviderStatusRaw { get; set; }

    /// <summary>
    /// Provider event ID from the last webhook. Used for quick replay check.
    /// Full event ledger stored in WebhookEvents.
    /// </summary>
    public string? ProviderEventId { get; set; }

    /// <summary>When the payment completed (succeeded or failed terminally).</summary>
    public DateTime? CompletedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    public virtual Payout? Payout { get; set; }

    /// <summary>Ledger of all webhook events received for this transaction.</summary>
    public virtual ICollection<PaymentWebhookEvent> WebhookEvents { get; set; } = new List<PaymentWebhookEvent>();
}
