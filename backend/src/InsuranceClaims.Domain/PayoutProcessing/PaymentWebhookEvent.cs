using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PayoutProcessing;

/// <summary>
/// Separate ledger for storing incoming webhook events from payment providers (e.g. PayPal, Mock).
///
/// A single transaction may receive multiple legitimate events over time
/// (e.g. batch.processing -> batch.success -> item.unclaimed -> item.succeeded).
///
/// Unique constraint on (Provider, ProviderEventId) guarantees replay safety and idempotency.
/// </summary>
public class PaymentWebhookEvent : BaseEntity
{
    /// <summary>Provider name (e.g., "PayPal", "Mock").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Unique provider event identifier (e.g. PayPal WH-..., mock evt-...).</summary>
    public string ProviderEventId { get; set; } = string.Empty;

    /// <summary>Associated internal PaymentTransaction ID, if matched.</summary>
    public Guid? PaymentTransactionId { get; set; }

    /// <summary>Event type string (e.g., "PAYMENT.PAYOUTS-ITEM.SUCCEEDED", "payment.succeeded").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Resource type (e.g., "payouts_item", "payouts").</summary>
    public string? ResourceType { get; set; }

    /// <summary>Human-readable summary of the event.</summary>
    public string? Summary { get; set; }

    /// <summary>Timestamp when webhook was received by the application.</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when event was processed.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Processing outcome: "Processed", "Duplicate", "TransactionNotFound", "AmountMismatch", "Invalid".</summary>
    public string ProcessingStatus { get; set; } = "Processed";

    /// <summary>Failure reason if processing was not successful.</summary>
    public string? FailureReason { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    public virtual PaymentTransaction? PaymentTransaction { get; set; }
}
