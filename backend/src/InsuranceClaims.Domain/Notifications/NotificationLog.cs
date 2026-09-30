using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.Notifications;

/// <summary>
/// Persistent, auditable record of every notification attempt.
///
/// Key design decisions:
/// - Non-authoritative: a failed notification never rolls back a successful
///   business transaction (claim submission, payout, etc.).
/// - Idempotent: the composite key (UserId + ClaimId + NotificationType) prevents
///   duplicate notifications on retries.
/// - Provider-independent: the Provider field records which implementation
///   (Mock, SendGrid, SES, etc.) dispatched the message.
/// </summary>
public class NotificationLog : BaseEntity
{
    /// <summary>Recipient user ID — always resolved server-side from authoritative data.</summary>
    public Guid UserId { get; set; }

    /// <summary>Related claim ID (nullable for system-wide or payout-only notifications).</summary>
    public Guid? ClaimId { get; set; }

    /// <summary>Related payout ID (nullable for claim-only notifications).</summary>
    public Guid? PayoutId { get; set; }

    /// <summary>
    /// Stable idempotency key for this notification event.
    /// E.g. "claim:{claimId}:submitted", "payout:{payoutId}:approved".
    /// Prevents duplicate dispatches on HTTP retries while allowing new later workflow events.
    /// </summary>
    public string NotificationKey { get; set; } = string.Empty;

    /// <summary>Business event that triggered this notification.</summary>
    public NotificationType NotificationType { get; set; }

    /// <summary>Delivery channel (Email, SMS, Push).</summary>
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;

    /// <summary>Recipient address (email, phone number, device token).</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>Email/notification subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Full message body (HTML or plain text).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Explicit delivery lifecycle status.</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    /// <summary>Whether the delivery attempt succeeded (backward-compatible; true when Status == Sent).</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the delivery failed (null on success).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Provider that handled the delivery (e.g., "Mock", "SendGrid").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider-specific message/transaction ID for traceability.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Timestamp when the delivery was successfully sent (null for failed/unsent).</summary>
    public DateTime? SentAt { get; set; }
}
