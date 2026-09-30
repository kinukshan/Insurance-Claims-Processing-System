using InsuranceClaims.Domain.Notifications;

namespace InsuranceClaims.Application.Notifications.Interfaces;

/// <summary>
/// Repository for NotificationLog persistence.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>Add a notification log entry.</summary>
    Task<NotificationLog> AddAsync(NotificationLog log);

    /// <summary>Get all logs for a specific user, ordered by most recent first.</summary>
    Task<List<NotificationLog>> GetByUserIdAsync(Guid userId);

    /// <summary>Get all logs for a specific claim, ordered by most recent first.</summary>
    Task<List<NotificationLog>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Get all logs for a specific payout, ordered by most recent first.</summary>
    Task<List<NotificationLog>> GetByPayoutIdAsync(Guid payoutId);

    /// <summary>
    /// Check if a notification with the given stable notification key already succeeded.
    /// Used for idempotency to prevent duplicate notifications on retries while allowing genuinely new workflow events.
    /// </summary>
    Task<bool> ExistsByKeyAsync(string notificationKey);

    /// <summary>
    /// Legacy check if a notification with the given composite key already exists and succeeded.
    /// </summary>
    Task<bool> ExistsAsync(Guid userId, Guid? claimId, NotificationType type);

    /// <summary>Get all notification logs with optional filtering.</summary>
    Task<List<NotificationLog>> GetAllAsync(int page = 1, int pageSize = 50);

    /// <summary>
    /// Atomically reserves delivery of a notification with the given NotificationKey.
    /// If an active reservation or completed delivery already exists, IsReserved is false.
    /// Handles database unique constraints and lease expiration (crash recovery) safely.
    /// </summary>
    Task<NotificationReservationResult> TryReserveAsync(NotificationLog draftLog, TimeSpan? leaseDuration = null);

    /// <summary>
    /// Marks an actively processing notification reservation as successfully Sent.
    /// </summary>
    Task<NotificationLog> MarkSentAsync(string notificationKey, string provider, string? providerMessageId);

    /// <summary>
    /// Marks an actively processing notification as Accepted by an external provider.
    /// This indicates the provider accepted the message but final delivery is not yet confirmed.
    /// </summary>
    Task<NotificationLog> MarkAcceptedAsync(string notificationKey, string provider, string? providerMessageId);

    /// <summary>
    /// Marks an actively processing notification reservation as Failed.
    /// </summary>
    Task<NotificationLog> MarkFailedAsync(string notificationKey, string provider, string? errorMessage);
}

/// <summary>
/// Result of an atomic notification reservation attempt.
/// </summary>
public record NotificationReservationResult(
    bool IsReserved,
    bool AlreadyDelivered,
    NotificationLog? Log
);
