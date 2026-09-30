using InsuranceClaims.Domain.Notifications;

namespace InsuranceClaims.Application.Notifications.Interfaces;

/// <summary>
/// High-level notification orchestrator that coordinates email composition,
/// delivery, and audit logging.
///
/// Design rules:
///   1. NON-AUTHORITATIVE: Notification failure MUST NEVER cause a business
///      transaction rollback (claim submission, payout approval, etc.).
///   2. IDEMPOTENT: Duplicate triggers for the same (UserId, ClaimId, NotificationType)
///      are silently ignored.
///   3. SERVER-SIDE RECIPIENTS: Recipient emails are always resolved from the
///      backend database, never from frontend input.
/// </summary>
public interface INotificationOrchestrator
{
    /// <summary>
    /// Send a notification for the given business event with a stable idempotency key.
    /// Returns true if delivery succeeded, false otherwise.
    /// NEVER throws — all errors are caught and logged.
    /// </summary>
    Task<bool> NotifyAsync(
        string notificationKey,
        Guid userId,
        string recipientEmail,
        Guid? claimId,
        NotificationType type,
        string? claimNumber = null,
        Guid? payoutId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy overload that derives a notification key from (userId, claimId, type).
    /// </summary>
    Task<bool> NotifyAsync(
        Guid userId,
        string recipientEmail,
        Guid? claimId,
        NotificationType type,
        string? claimNumber = null,
        CancellationToken cancellationToken = default);
}
