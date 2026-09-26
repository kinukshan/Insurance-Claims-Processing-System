using System.Collections.Concurrent;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Application.Notifications.Services;

/// <summary>
/// Orchestrates email composition, delivery via IEmailService, and
/// persistent audit logging via INotificationLogRepository.
///
/// NON-AUTHORITATIVE: This service catches ALL exceptions internally.
/// A failed notification NEVER propagates up to the caller and NEVER
/// triggers a rollback of the parent business transaction.
/// </summary>
public class NotificationOrchestrator : INotificationOrchestrator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private readonly IEmailService _emailService;
    private readonly INotificationLogRepository _logRepository;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(
        IEmailService emailService,
        INotificationLogRepository logRepository,
        ILogger<NotificationOrchestrator> logger)
    {
        _emailService = emailService;
        _logRepository = logRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> NotifyAsync(
        string notificationKey,
        Guid userId,
        string recipientEmail,
        Guid? claimId,
        NotificationType type,
        string? claimNumber = null,
        Guid? payoutId = null,
        CancellationToken cancellationToken = default)
    {
        var sem = _locks.GetOrAdd(notificationKey, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken);
        try
        {
            // 1. Fast idempotency check — skip if already sent successfully
            var alreadySent = await _logRepository.ExistsByKeyAsync(notificationKey);
            if (alreadySent)
            {
                _logger.LogInformation(
                    "Notification {Key} ({Type}) for user {UserId} already sent — skipping duplicate",
                    notificationKey, type, userId);
                return true; // Treat as success (already delivered)
            }

            // 2. Compose the email
            var (subject, body) = ComposeEmail(type, claimNumber, claimId, payoutId);

            var draftLog = new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ClaimId = claimId,
                PayoutId = payoutId,
                NotificationKey = notificationKey,
                NotificationType = type,
                Channel = NotificationChannel.Email,
                Recipient = recipientEmail,
                Subject = subject,
                Body = body,
                Status = NotificationStatus.Processing,
                Success = false,
                ErrorMessage = null,
                Provider = "Mock",
                ProviderMessageId = null,
                SentAt = null
            };

            // 3. Atomically reserve notification delivery in database
            var reservation = await _logRepository.TryReserveAsync(draftLog);
            if (reservation.AlreadyDelivered)
            {
                _logger.LogInformation(
                    "Notification {Key} ({Type}) for user {UserId} already delivered — skipping duplicate",
                    notificationKey, type, userId);
                return true;
            }

            if (!reservation.IsReserved)
            {
                _logger.LogInformation(
                    "Notification {Key} ({Type}) is actively reserved or leased by another instance — skipping duplicate dispatch",
                    notificationKey, type);
                return true;
            }

            // 4. Send via provider — only the winner of the database reservation sends
            var message = new EmailMessage
            {
                To = recipientEmail,
                Subject = subject,
                Body = body,
                IdempotencyKey = notificationKey
            };

            EmailSendResult result;
            try
            {
                result = await _emailService.SendAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Provider send threw exception for {Key} ({Type}) to {Recipient}",
                    notificationKey, type, recipientEmail);

                await _logRepository.MarkFailedAsync(notificationKey, "Unknown", ex.Message);
                return false;
            }

            // 5. Persist final delivery status
            if (result.Success)
            {
                // Mock provider confirms delivery; external providers only confirm acceptance
                if (string.Equals(result.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
                {
                    await _logRepository.MarkSentAsync(notificationKey, result.Provider, result.MessageId);
                }
                else
                {
                    await _logRepository.MarkAcceptedAsync(notificationKey, result.Provider, result.MessageId);
                }
                _logger.LogInformation(
                    "Notification {Key} ({Type}) sent to {Recipient} via {Provider} (MessageId: {MessageId})",
                    notificationKey, type, recipientEmail, result.Provider, result.MessageId);
                return true;
            }
            else
            {
                await _logRepository.MarkFailedAsync(notificationKey, result.Provider, result.ErrorMessage);
                _logger.LogWarning(
                    "Notification {Key} ({Type}) to {Recipient} via {Provider} FAILED: {Error}",
                    notificationKey, type, recipientEmail, result.Provider, result.ErrorMessage);
                return false;
            }
        }
        catch (DuplicateNotificationException ex)
        {
            _logger.LogInformation(
                "Notification {Key} ({Type}) already processed by concurrent race — skipping duplicate: {Message}",
                notificationKey, type, ex.Message);
            return true; // Treat as success (already processed)
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "Notification {Key} ({Type}) unique constraint hit during concurrent race — treating as already processed: {Message}",
                notificationKey, type, ex.Message);
            return true; // Treat as success (already processed)
        }
        catch (Exception ex)
        {
            // NON-AUTHORITATIVE: swallow all exceptions — log and return false
            _logger.LogError(ex,
                "Notification orchestration failed for {Key} ({Type}) to user {UserId}. " +
                "This does NOT affect the parent business transaction.",
                notificationKey, type, userId);

            // Best-effort: try to mark failure log
            try
            {
                await _logRepository.MarkFailedAsync(notificationKey, "Unknown", ex.Message);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to persist notification failure log");
            }

            return false;
        }
        finally
        {
            sem.Release();
            if (sem.CurrentCount == 1)
            {
                _locks.TryRemove(notificationKey, out _);
            }
        }
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is DuplicateNotificationException)
                return true;

            var msg = current.Message;
            if (msg.Contains("23505") ||
                msg.Contains("IX_NotificationLogs_NotificationKey", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }
        return false;
    }

    /// <inheritdoc />
    public Task<bool> NotifyAsync(
        Guid userId,
        string recipientEmail,
        Guid? claimId,
        NotificationType type,
        string? claimNumber = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"user:{userId}:claim:{claimId}:{type}";
        return NotifyAsync(key, userId, recipientEmail, claimId, type, claimNumber, null, cancellationToken);
    }

    /// <summary>
    /// Compose subject and body for each notification type.
    /// Templates are intentionally simple and hardcoded for Phase 1.
    /// </summary>
    private static (string Subject, string Body) ComposeEmail(
        NotificationType type, string? claimNumber, Guid? claimId, Guid? payoutId = null)
    {
        var claimRef = claimNumber ?? claimId?.ToString("N")[..8] ?? payoutId?.ToString("N")[..8] ?? "N/A";

        return type switch
        {
            NotificationType.ClaimSubmitted => (
                $"Claim {claimRef} — Submitted Successfully",
                $"<p>Your insurance claim <strong>{claimRef}</strong> has been submitted successfully and is now under review.</p>" +
                "<p>You will receive updates as your claim progresses through the review process.</p>"
            ),
            NotificationType.ClaimApproved => (
                $"Claim {claimRef} — Approved",
                $"<p>Great news! Your insurance claim <strong>{claimRef}</strong> has been approved.</p>" +
                "<p>Payout processing will begin shortly.</p>"
            ),
            NotificationType.ClaimRejected => (
                $"Claim {claimRef} — Update",
                $"<p>An update is available regarding your claim <strong>{claimRef}</strong>.</p>" +
                "<p>Please log in to your portal or contact your representative for details.</p>"
            ),
            NotificationType.ClaimWithdrawn => (
                $"Claim {claimRef} — Withdrawn",
                $"<p>Your insurance claim <strong>{claimRef}</strong> has been withdrawn as requested.</p>"
            ),
            NotificationType.DocumentsVerified or NotificationType.DocumentVerificationComplete => (
                $"Claim {claimRef} — Documents Verified",
                $"<p>All required documents for claim <strong>{claimRef}</strong> have been verified successfully.</p>" +
                "<p>Your claim is moving forward to the next stage.</p>"
            ),
            NotificationType.DocumentsNeedReview => (
                $"Claim {claimRef} — Documents Under Review",
                $"<p>The documents submitted for claim <strong>{claimRef}</strong> are currently under detailed review by our claims team.</p>" +
                "<p>We will contact you if any further details are needed.</p>"
            ),
            NotificationType.AdditionalDocumentsRequired => (
                $"Claim {claimRef} — Additional Documents Required",
                $"<p>Additional documents are required for your claim <strong>{claimRef}</strong>.</p>" +
                "<p>Please log in to the portal and upload the requested documents.</p>"
            ),
            NotificationType.RiskAssessmentNeedsReview or NotificationType.RiskAssessmentComplete => (
                $"Claim {claimRef} — Review in Progress",
                $"<p>Your claim <strong>{claimRef}</strong> is currently undergoing routine review as part of standard claims processing.</p>" +
                "<p>No action is required from you at this time. We will notify you once this step is complete.</p>"
            ),
            NotificationType.PayoutPendingApproval => (
                $"Payout for Claim {claimRef} — Pending Review",
                $"<p>A payout calculation for claim <strong>{claimRef}</strong> has been prepared and is currently pending final authorization.</p>"
            ),
            NotificationType.PayoutApproved => (
                $"Payout for Claim {claimRef} — Approved",
                $"<p>The payout for your claim <strong>{claimRef}</strong> has been approved and authorized for disbursement.</p>" +
                "<p>Payment processing will initiate shortly.</p>"
            ),
            NotificationType.PayoutRejected => (
                $"Payout for Claim {claimRef} — Rejected",
                $"<p>The payout proposal for your claim <strong>{claimRef}</strong> has been rejected by the reviewer.</p>" +
                "<p>Please contact your claims adjuster for further details.</p>"
            ),
            NotificationType.PayoutCompleted => (
                $"Payout for Claim {claimRef} — Payout Completed",
                $"<p>The payout for your claim <strong>{claimRef}</strong> has been successfully processed and disbursed.</p>" +
                "<p>Please allow standard banking transit time for the funds to reflect in your registered account.</p>"
            ),
            NotificationType.PayoutFailed => (
                $"Payout for Claim {claimRef} — Processing Issue",
                $"<p>We encountered an issue while processing the disbursement for your claim <strong>{claimRef}</strong>.</p>" +
                "<p>Our finance team has been notified and is working to resolve this promptly. We will contact you if any updated payment details are required.</p>"
            ),
            _ => (
                $"Claim {claimRef} — Notification",
                $"<p>There is an update regarding your claim <strong>{claimRef}</strong>.</p>"
            )
        };
    }
}
