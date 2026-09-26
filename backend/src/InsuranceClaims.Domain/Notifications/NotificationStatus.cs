namespace InsuranceClaims.Domain.Notifications;

/// <summary>
/// Explicit lifecycle delivery status for a notification attempt.
/// </summary>
public enum NotificationStatus
{
    /// <summary>Reserved, awaiting dispatch.</summary>
    Pending = 0,

    /// <summary>Currently acquired and in-flight with the delivery provider.</summary>
    Processing = 1,

    /// <summary>Successfully delivered by the provider (Mock or confirmed delivery).</summary>
    Sent = 2,

    /// <summary>Delivery failed or encountered an error.</summary>
    Failed = 3,

    /// <summary>
    /// Provider accepted the message for delivery but final delivery is not yet confirmed.
    /// Used for external providers like Resend where API acceptance ≠ inbox delivery.
    /// Displayed as "Accepted" in UI, not "Delivered".
    /// </summary>
    Accepted = 4
}
