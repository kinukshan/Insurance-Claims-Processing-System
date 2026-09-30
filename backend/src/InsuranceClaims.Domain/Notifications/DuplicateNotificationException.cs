namespace InsuranceClaims.Domain.Notifications;

/// <summary>
/// Thrown when an attempt is made to insert or process a notification
/// with a NotificationKey that already exists or is being concurrently processed.
/// </summary>
public class DuplicateNotificationException : Exception
{
    public DuplicateNotificationException(string message) : base(message)
    {
    }

    public DuplicateNotificationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
