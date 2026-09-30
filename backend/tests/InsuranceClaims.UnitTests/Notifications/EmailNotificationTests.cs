using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.Notifications.Services;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Infrastructure.ExternalServices.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceClaims.UnitTests.Notifications;

/// <summary>
/// Comprehensive tests for the email notification infrastructure.
/// Covers: MockEmailService, NotificationOrchestrator, idempotency, failure handling,
/// non-authoritative behavior, and email composition.
/// </summary>
public class EmailNotificationTests
{
    // ───── MockEmailService Tests ─────────────────────────────────────

    [Fact]
    public async Task MockEmailService_Success_ReturnsSuccessResult()
    {
        // Arrange
        var svc = new MockEmailService("success");
        var msg = new EmailMessage
        {
            To = "user@example.com",
            Subject = "Test Subject",
            Body = "<p>Test body</p>"
        };

        // Act
        var result = await svc.SendAsync(msg);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Mock", result.Provider);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("MOCK-EMAIL-", result.MessageId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task MockEmailService_Failure_ReturnsFailureResult()
    {
        // Arrange
        var svc = new MockEmailService("failure");
        var msg = new EmailMessage
        {
            To = "user@example.com",
            Subject = "Test Subject",
            Body = "<p>Test body</p>"
        };

        // Act
        var result = await svc.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Mock", result.Provider);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Simulated", result.ErrorMessage);
    }

    [Fact]
    public async Task MockEmailService_RecordsAllSentMessages()
    {
        // Arrange
        var svc = new MockEmailService("success");

        // Act
        await svc.SendAsync(new EmailMessage { To = "a@b.com", Subject = "S1", Body = "B1" });
        await svc.SendAsync(new EmailMessage { To = "c@d.com", Subject = "S2", Body = "B2" });

        // Assert
        var sent = svc.GetSentMessages();
        Assert.Equal(2, sent.Count);
        Assert.Equal("a@b.com", sent[0].To);
        Assert.Equal("c@d.com", sent[1].To);
    }

    [Fact]
    public async Task MockEmailService_ClearSentMessages_ClearsHistory()
    {
        // Arrange
        var svc = new MockEmailService("success");
        await svc.SendAsync(new EmailMessage { To = "a@b.com", Subject = "S", Body = "B" });

        // Act
        svc.ClearSentMessages();

        // Assert
        Assert.Empty(svc.GetSentMessages());
    }

    [Fact]
    public void MockEmailService_ThrowsOnNullMessage()
    {
        var svc = new MockEmailService("success");
        Assert.ThrowsAsync<ArgumentNullException>(() => svc.SendAsync(null!));
    }

    // ───── NotificationOrchestrator Tests ─────────────────────────────

    [Fact]
    public async Task Orchestrator_SuccessfulNotification_ReturnsTrue()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        var result = await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.ClaimSubmitted, "CLM-001");

        // Assert
        Assert.True(result);
        Assert.Single(logRepo.Logs);
        Assert.True(logRepo.Logs[0].Success);
        Assert.Equal("Mock", logRepo.Logs[0].Provider);
    }

    [Fact]
    public async Task Orchestrator_FailedDelivery_ReturnsFalseAndLogs()
    {
        // Arrange
        var emailService = new MockEmailService("failure");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        var result = await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.ClaimSubmitted, "CLM-002");

        // Assert
        Assert.False(result);
        Assert.Single(logRepo.Logs);
        Assert.False(logRepo.Logs[0].Success);
        Assert.NotNull(logRepo.Logs[0].ErrorMessage);
    }

    [Fact]
    public async Task Orchestrator_Idempotency_SkipsDuplicateNotification()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        // Act — send the same notification twice
        var first = await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-003");

        var second = await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-003");

        // Assert
        Assert.True(first);
        Assert.True(second); // Returns true (already sent)
        Assert.Single(logRepo.Logs); // Only one log entry
    }

    [Fact]
    public async Task Orchestrator_DifferentTypes_NotDuplicate()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        // Act — send two different notification types for the same claim
        await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-004");

        await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimApproved, "CLM-004");

        // Assert — both should be logged
        Assert.Equal(2, logRepo.Logs.Count);
    }

    [Fact]
    public async Task Orchestrator_NonAuthoritative_NeverThrows()
    {
        // Arrange — email service that throws
        var throwingService = new ThrowingEmailService();
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(throwingService, logRepo, logger);

        // Act & Assert — should not throw
        var result = await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.ClaimSubmitted, "CLM-005");

        Assert.False(result);
        // Should still log the failure
        Assert.Single(logRepo.Logs);
        Assert.False(logRepo.Logs[0].Success);
    }

    [Fact]
    public async Task Orchestrator_ComposesCorrectSubject_ClaimSubmitted()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.ClaimSubmitted, "CLM-100");

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("CLM-100", log.Subject);
        Assert.Contains("Submitted", log.Subject);
        Assert.Contains("CLM-100", log.Body);
    }

    [Fact]
    public async Task Orchestrator_ComposesCorrectSubject_PayoutCompleted()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.PayoutCompleted, "CLM-200");

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("Payout Completed", log.Subject);
    }

    [Fact]
    public async Task Orchestrator_SetsCorrectChannelAndProvider()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        await orchestrator.NotifyAsync(
            Guid.NewGuid(), "user@example.com", Guid.NewGuid(),
            NotificationType.ClaimApproved, "CLM-300");

        // Assert
        var log = logRepo.Logs[0];
        Assert.Equal(NotificationChannel.Email, log.Channel);
        Assert.Equal("Mock", log.Provider);
        Assert.Equal("user@example.com", log.Recipient);
    }

    [Fact]
    public async Task Orchestrator_AllNotificationTypes_ComposeValidEmails()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act & Assert — every notification type should produce a non-empty subject and body
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
        {
            var userId = Guid.NewGuid();
            var claimId = Guid.NewGuid();
            await orchestrator.NotifyAsync(userId, "test@example.com", claimId, type, "CLM-ALL");
        }

        // All types should have been logged
        Assert.Equal(Enum.GetValues<NotificationType>().Length, logRepo.Logs.Count);
        foreach (var log in logRepo.Logs)
        {
            Assert.False(string.IsNullOrWhiteSpace(log.Subject), $"Subject empty for {log.NotificationType}");
            Assert.False(string.IsNullOrWhiteSpace(log.Body), $"Body empty for {log.NotificationType}");
        }
    }

    [Fact]
    public async Task Orchestrator_StableNotificationKey_PreventsDuplicateSends_AllowsNewWorkflowEvents()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var keySubmit = $"claim:{claimId}:submitted";
        var keyDocReview = DocumentNotificationKeys.ForAdditionalDocsRequired(claimId, new[] { "MedicalBill", "PoliceReport" });

        // Act 1: Send submitted
        var res1 = await orchestrator.NotifyAsync(keySubmit, userId, "u@example.com", claimId, NotificationType.ClaimSubmitted, "CLM-1");
        Assert.True(res1);
        Assert.Single(logRepo.Logs);
        Assert.Equal(keySubmit, logRepo.Logs[0].NotificationKey);

        // Act 2: Duplicate retry of submitted -> skipped via stable key
        var res2 = await orchestrator.NotifyAsync(keySubmit, userId, "u@example.com", claimId, NotificationType.ClaimSubmitted, "CLM-1");
        Assert.True(res2);
        Assert.Single(logRepo.Logs); // Still 1, no duplicate sent

        // Act 3: Genuinely new workflow event -> allowed
        var res3 = await orchestrator.NotifyAsync(keyDocReview, userId, "u@example.com", claimId, NotificationType.AdditionalDocumentsRequired, "CLM-1");
        Assert.True(res3);
        Assert.Equal(2, logRepo.Logs.Count);
        Assert.Equal(keyDocReview, logRepo.Logs[1].NotificationKey);
    }

    [Fact]
    public async Task Orchestrator_PayoutApproved_WordingSaysApprovedNotPaid()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var payoutId = Guid.NewGuid();

        // Act
        await orchestrator.NotifyAsync(
            $"payout:{payoutId}:approved",
            Guid.NewGuid(), "u@example.com", Guid.NewGuid(),
            NotificationType.PayoutApproved, "CLM-APP", payoutId);

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("Approved", log.Subject);
        Assert.Contains("approved and authorized", log.Body);
        Assert.DoesNotContain("Payment Completed", log.Subject);
        Assert.Equal(payoutId, log.PayoutId);
    }

    [Fact]
    public async Task Orchestrator_PayoutCompleted_WordingSaysPaymentCompleted()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var payoutId = Guid.NewGuid();

        // Act
        await orchestrator.NotifyAsync(
            $"payout:{payoutId}:completed",
            Guid.NewGuid(), "u@example.com", Guid.NewGuid(),
            NotificationType.PayoutCompleted, "CLM-COMP", payoutId);

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("Payout Completed", log.Subject);
        Assert.Contains("successfully processed and disbursed", log.Body);
        Assert.Equal(payoutId, log.PayoutId);
    }

    [Fact]
    public async Task Orchestrator_PayoutFailed_HasSafeWordingWithoutRawProviderErrors()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        var payoutId = Guid.NewGuid();

        // Act
        await orchestrator.NotifyAsync(
            $"payout:{payoutId}:failed:tx-1",
            Guid.NewGuid(), "u@example.com", Guid.NewGuid(),
            NotificationType.PayoutFailed, "CLM-FAIL", payoutId);

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("Processing Issue", log.Subject);
        Assert.DoesNotContain("Exception", log.Body);
        Assert.DoesNotContain("SQL", log.Body);
        Assert.Contains("finance team has been notified", log.Body);
        Assert.Equal(payoutId, log.PayoutId);
    }

    [Fact]
    public async Task Orchestrator_RiskAssessmentNeedsReview_NeutralPolicyholderWording()
    {
        // Arrange
        var emailService = new MockEmailService("success");
        var logRepo = new InMemoryNotificationLogRepository();
        var logger = NullLogger<NotificationOrchestrator>.Instance;
        var orchestrator = new NotificationOrchestrator(emailService, logRepo, logger);

        // Act
        await orchestrator.NotifyAsync(
            "claim:risk-key",
            Guid.NewGuid(), "u@example.com", Guid.NewGuid(),
            NotificationType.RiskAssessmentNeedsReview, "CLM-RISK");

        // Assert
        var log = logRepo.Logs[0];
        Assert.Contains("Review in Progress", log.Subject);
        Assert.Contains("routine review", log.Body);
        Assert.DoesNotContain("fraud", log.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", log.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI", log.Body);
    }

    // ───── Test Doubles ──────────────────────────────────────────────

    /// <summary>In-memory notification log repository for testing.</summary>
    public class InMemoryNotificationLogRepository : INotificationLogRepository
    {
        public List<NotificationLog> Logs { get; } = new();

        public Task<NotificationLog> AddAsync(NotificationLog log)
        {
            var existing = Logs.FirstOrDefault(l => l.NotificationKey == log.NotificationKey);
            if (existing != null)
            {
                if (!existing.Success && log.Success)
                {
                    existing.Success = true;
                    existing.SentAt = log.SentAt ?? DateTime.UtcNow;
                    existing.Provider = log.Provider;
                    existing.ProviderMessageId = log.ProviderMessageId;
                    existing.ErrorMessage = null;
                }
                return Task.FromResult(existing);
            }
            Logs.Add(log);
            return Task.FromResult(log);
        }

        public Task<List<NotificationLog>> GetByUserIdAsync(Guid userId) =>
            Task.FromResult(Logs.Where(l => l.UserId == userId).ToList());

        public Task<List<NotificationLog>> GetByClaimIdAsync(Guid claimId) =>
            Task.FromResult(Logs.Where(l => l.ClaimId == claimId).ToList());

        public Task<List<NotificationLog>> GetByPayoutIdAsync(Guid payoutId) =>
            Task.FromResult(Logs.Where(l => l.PayoutId == payoutId).ToList());

        public Task<bool> ExistsByKeyAsync(string notificationKey) =>
            Task.FromResult(Logs.Any(l => l.NotificationKey == notificationKey && l.Success));

        public Task<bool> ExistsAsync(Guid userId, Guid? claimId, NotificationType type) =>
            Task.FromResult(Logs.Any(l =>
                l.UserId == userId && l.ClaimId == claimId && l.NotificationType == type && l.Success));

        public Task<List<NotificationLog>> GetAllAsync(int page = 1, int pageSize = 50) =>
            Task.FromResult(Logs.Skip((page - 1) * pageSize).Take(pageSize).ToList());

        public Task<NotificationReservationResult> TryReserveAsync(NotificationLog draftLog, TimeSpan? leaseDuration = null)
        {
            var existing = Logs.FirstOrDefault(l => l.NotificationKey == draftLog.NotificationKey);
            if (existing != null)
            {
                if (existing.Status == NotificationStatus.Sent || existing.Success)
                {
                    return Task.FromResult(new NotificationReservationResult(false, true, existing));
                }

                if (existing.Status == NotificationStatus.Processing)
                {
                    var isExpired = DateTime.UtcNow - existing.UpdatedAt > (leaseDuration ?? TimeSpan.FromMinutes(5));
                    if (!isExpired)
                    {
                        return Task.FromResult(new NotificationReservationResult(false, false, existing));
                    }
                }

                existing.Status = NotificationStatus.Processing;
                existing.Success = false;
                existing.SentAt = null;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.Provider = draftLog.Provider;
                existing.Recipient = draftLog.Recipient;
                existing.Subject = draftLog.Subject;
                existing.Body = draftLog.Body;
                return Task.FromResult(new NotificationReservationResult(true, false, existing));
            }

            draftLog.Status = NotificationStatus.Processing;
            draftLog.Success = false;
            draftLog.SentAt = null;
            draftLog.CreatedAt = DateTime.UtcNow;
            draftLog.UpdatedAt = DateTime.UtcNow;
            Logs.Add(draftLog);
            return Task.FromResult(new NotificationReservationResult(true, false, draftLog));
        }

        public Task<NotificationLog> MarkSentAsync(string notificationKey, string provider, string? providerMessageId)
        {
            var log = Logs.First(l => l.NotificationKey == notificationKey);
            log.Status = NotificationStatus.Sent;
            log.Success = true;
            log.SentAt = DateTime.UtcNow;
            log.UpdatedAt = DateTime.UtcNow;
            log.Provider = provider;
            log.ProviderMessageId = providerMessageId;
            log.ErrorMessage = null;
            return Task.FromResult(log);
        }

        public Task<NotificationLog> MarkAcceptedAsync(string notificationKey, string provider, string? providerMessageId)
        {
            var log = Logs.First(l => l.NotificationKey == notificationKey);
            log.Status = NotificationStatus.Accepted;
            log.Success = true;
            log.SentAt = DateTime.UtcNow;
            log.UpdatedAt = DateTime.UtcNow;
            log.Provider = provider;
            log.ProviderMessageId = providerMessageId;
            log.ErrorMessage = null;
            return Task.FromResult(log);
        }

        public Task<NotificationLog> MarkFailedAsync(string notificationKey, string provider, string? errorMessage)
        {
            var log = Logs.First(l => l.NotificationKey == notificationKey);
            log.Status = NotificationStatus.Failed;
            log.Success = false;
            log.SentAt = null;
            log.UpdatedAt = DateTime.UtcNow;
            log.Provider = provider;
            log.ErrorMessage = errorMessage;
            return Task.FromResult(log);
        }
    }

    /// <summary>Email service that always throws, for testing non-authoritative behavior.</summary>
    private class ThrowingEmailService : IEmailService
    {
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated catastrophic email failure");
        }
    }
}
