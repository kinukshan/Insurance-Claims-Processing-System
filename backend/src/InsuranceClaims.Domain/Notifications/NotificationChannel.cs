namespace InsuranceClaims.Domain.Notifications;

/// <summary>
/// Delivery channel for a notification.
/// Currently only Email is implemented; SMS/Push are reserved for future phases.
/// </summary>
public enum NotificationChannel
{
    Email = 0,
    Sms = 1,
    Push = 2
}
