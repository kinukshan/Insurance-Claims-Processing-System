using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.Notifications.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.ExternalServices.Email;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.UnitTests.ClaimsManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.Notifications;

/// <summary>
/// Concurrency and idempotency test suite for notification processing.
/// Verifies:
/// 1. Two concurrent NotifyAsync calls with same NotificationKey -> one NotificationLog only.
/// 2. Repeated identical claim submission retry -> one ClaimSubmitted notification.
/// 3. Two separate document verification cycles with same document count -> two different notification events.
/// 4. Two AdditionalDocumentsRequired events with same missing count but different workflow occurrence -> both allowed.
/// 5. SentAt semantics: null for failed/unsent, actual timestamp only when successfully sent, CreatedAt preserved.
/// 6. Postgres / DB unique constraint races caught safely without 500.
/// </summary>
public class NotificationConcurrencyIdempotencyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationLogRepository _logRepository;
    private readonly MockEmailService _emailService;
    private readonly NotificationOrchestrator _orchestrator;

    public NotificationConcurrencyIdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NotifConcurrencyDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _logRepository = new NotificationLogRepository(_context);
        _emailService = new MockEmailService("success");
        _orchestrator = new NotificationOrchestrator(
            _emailService,
            _logRepository,
            NullLogger<NotificationOrchestrator>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task TwoConcurrentNotifyAsyncCalls_WithSameNotificationKey_ResultsInOneNotificationLogOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:submitted";

        // Act — Fire two concurrent NotifyAsync calls with the exact same NotificationKey
        var task1 = _orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-CONCURRENT-1");

        var task2 = _orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-CONCURRENT-1");

        var results = await Task.WhenAll(task1, task2);

        // Assert — Both calls report success (no 500, no failure thrown)
        Assert.True(results[0]);
        Assert.True(results[1]);

        // Only ONE NotificationLog exists in the database
        var logs = await _context.NotificationLogs
            .Where(n => n.NotificationKey == notificationKey)
            .ToListAsync();

        Assert.Single(logs);
        Assert.Equal(notificationKey, logs[0].NotificationKey);
        Assert.True(logs[0].Success);
        Assert.NotNull(logs[0].SentAt);

        // Only ONE email was actually dispatched by the provider
        Assert.Single(_emailService.GetSentMessages());
    }

    [Fact]
    public async Task RepeatedIdenticalClaimSubmissionRetry_CreatesOnlyOneClaimSubmittedNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:submitted";

        // Act 1: Initial claim submission notification
        var initialResult = await _orchestrator.NotifyAsync(
            notificationKey, userId, "policyholder@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-RETRY-001");

        Assert.True(initialResult);
        Assert.Single(_emailService.GetSentMessages());

        // Act 2: Identical retry (e.g., client network timeout retry)
        var retryResult = await _orchestrator.NotifyAsync(
            notificationKey, userId, "policyholder@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-RETRY-001");

        // Assert — Retry succeeds gracefully via idempotency check
        Assert.True(retryResult);

        // Database contains only ONE log entry
        var logs = await _context.NotificationLogs
            .Where(n => n.ClaimId == claimId)
            .ToListAsync();

        Assert.Single(logs);
        Assert.Equal(NotificationType.ClaimSubmitted, logs[0].NotificationType);
        Assert.Equal(notificationKey, logs[0].NotificationKey);

        // Email service still only sent one message (no duplicate email dispatched)
        Assert.Single(_emailService.GetSentMessages());
    }

    [Fact]
    public async Task TwoSeparateDocumentVerificationCycles_WithSameDocumentCount_CreatesTwoDifferentNotificationEvents()
    {
        // Arrange — Two separate verification cycles for the same claim, both with 1 document verified
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        var doc1 = new ClaimDocumentDto(
            Guid.NewGuid(), claimId, "DocA.pdf", "/files/DocA.pdf",
            "MedicalReport", "application/pdf", 1024, DateTime.UtcNow.AddHours(-2), "Verified");

        var doc2 = new ClaimDocumentDto(
            Guid.NewGuid(), claimId, "DocB.pdf", "/files/DocB.pdf",
            "RepairEstimate", "application/pdf", 2048, DateTime.UtcNow, "Verified");

        // Cycle 1: Doc count = 1
        var keyCycle1 = DocumentNotificationKeys.ForDocumentsVerified(
            claimId, new List<ClaimDocumentDto> { doc1 });

        // Cycle 2: Doc count is STILL 1, but represents a distinct document verification occurrence
        var keyCycle2 = DocumentNotificationKeys.ForDocumentsVerified(
            claimId, new List<ClaimDocumentDto> { doc2 });

        // Assert: Keys must NOT be identical despite having the identical document count (1)
        Assert.NotEqual(keyCycle1, keyCycle2);
        Assert.StartsWith($"claim:{claimId}:docs-verified:", keyCycle1);
        Assert.StartsWith($"claim:{claimId}:docs-verified:", keyCycle2);

        // Act — Dispatch both cycles
        var res1 = await _orchestrator.NotifyAsync(
            keyCycle1, userId, "user@example.com", claimId,
            NotificationType.DocumentsVerified, "CLM-DOC-001");

        var res2 = await _orchestrator.NotifyAsync(
            keyCycle2, userId, "user@example.com", claimId,
            NotificationType.DocumentsVerified, "CLM-DOC-001");

        // Assert — Both legitimate events are allowed and logged as distinct events
        Assert.True(res1);
        Assert.True(res2);

        var logs = await _context.NotificationLogs
            .Where(n => n.ClaimId == claimId && n.NotificationType == NotificationType.DocumentsVerified)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal(keyCycle1, logs[0].NotificationKey);
        Assert.Equal(keyCycle2, logs[1].NotificationKey);
    }

    [Fact]
    public async Task TwoAdditionalDocumentsRequiredEvents_WithSameMissingCount_ButDifferentWorkflowOccurrence_BothAllowed()
    {
        // Arrange — Two separate verification runs for the same claim, both missing exactly 1 document
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        var existingDocs = new List<ClaimDocumentDto>
        {
            new(Guid.NewGuid(), claimId, "Existing.pdf", "/files/Existing.pdf", "ID", "application/pdf", 100, DateTime.UtcNow, "Verified")
        };

        // Occurrence 1: Missing "PoliceReport" (missing count = 1)
        var keyOccurrence1 = DocumentNotificationKeys.ForAdditionalDocsRequired(
            claimId, new[] { "PoliceReport" }, existingDocs, occurrenceId: "occurrence-run-1");

        // Occurrence 2: Missing "RepairEstimate" (missing count = 1, same count but different workflow occurrence)
        var keyOccurrence2 = DocumentNotificationKeys.ForAdditionalDocsRequired(
            claimId, new[] { "RepairEstimate" }, existingDocs, occurrenceId: "occurrence-run-2");

        // Assert: Keys are distinct
        Assert.NotEqual(keyOccurrence1, keyOccurrence2);

        // Act 1: Send notification for Occurrence 1
        var res1 = await _orchestrator.NotifyAsync(
            keyOccurrence1, userId, "user@example.com", claimId,
            NotificationType.AdditionalDocumentsRequired, "CLM-ADD-001");

        // Act 2: Send notification for Occurrence 2 (new occurrence allowed)
        var res2 = await _orchestrator.NotifyAsync(
            keyOccurrence2, userId, "user@example.com", claimId,
            NotificationType.AdditionalDocumentsRequired, "CLM-ADD-001");

        // Act 3: Identical retry of Occurrence 1 (HTTP retry)
        var resRetry = await _orchestrator.NotifyAsync(
            keyOccurrence1, userId, "user@example.com", claimId,
            NotificationType.AdditionalDocumentsRequired, "CLM-ADD-001");

        // Assert
        Assert.True(res1);
        Assert.True(res2);
        Assert.True(resRetry); // Retry handled gracefully

        // Database contains exactly 2 logs (Occurrence 1 and Occurrence 2), retry did not add a 3rd
        var logs = await _context.NotificationLogs
            .Where(n => n.ClaimId == claimId && n.NotificationType == NotificationType.AdditionalDocumentsRequired)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal(keyOccurrence1, logs[0].NotificationKey);
        Assert.Equal(keyOccurrence2, logs[1].NotificationKey);
    }

    [Fact]
    public async Task SentAt_Semantics_NullForFailed_TimestampForSuccess_PreservesCreatedAtOnRetry()
    {
        // Arrange
        var failingEmailService = new MockEmailService("failure");
        var orchestratorWithFailingEmail = new NotificationOrchestrator(
            failingEmailService,
            _logRepository,
            NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:payout-failed";

        // Act 1: Attempt send with failing email service
        var failResult = await orchestratorWithFailingEmail.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.PayoutFailed, "CLM-FAIL-01");

        // Assert 1: NotifyAsync returns false, log is persisted with SentAt = null
        Assert.False(failResult);

        var failedLog = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        Assert.NotNull(failedLog);
        Assert.False(failedLog.Success);
        Assert.Null(failedLog.SentAt);
        Assert.NotNull(failedLog.ErrorMessage);

        var originalCreatedAt = failedLog.CreatedAt;
        Assert.NotEqual(default, originalCreatedAt);

        // Act 2: Retry with successful email service (preserving CreatedAt)
        var successResult = await _orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.PayoutFailed, "CLM-FAIL-01");

        // Assert 2: Successful retry updates SentAt with actual timestamp and preserves CreatedAt
        Assert.True(successResult);

        var updatedLog = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        Assert.NotNull(updatedLog);
        Assert.True(updatedLog.Success);
        Assert.NotNull(updatedLog.SentAt);
        Assert.Null(updatedLog.ErrorMessage);
        Assert.Equal(originalCreatedAt, updatedLog.CreatedAt);
    }

    [Fact]
    public async Task DatabaseUniqueConstraint_PostgresUniqueViolationRace_HandledGracefullyWithout500()
    {
        // Arrange — Repository double that simulates Postgres unique violation (SqlState 23505)
        var throwingRepo = new PostgresUniqueViolationMockRepository();
        var orchestrator = new NotificationOrchestrator(
            _emailService,
            throwingRepo,
            NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:unique-race";

        // Act — NotifyAsync encounters a Postgres unique constraint race
        var result = await orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-RACE-001");

        // Assert — Handled safely without 500, treated as already processed
        Assert.True(result);
        Assert.Equal(1, throwingRepo.AddCallCount);
    }

    [Fact]
    public async Task TwoConcurrentRequests_WithSameNotificationKey_SeparateDbContexts_RealRelationalUniqueConstraint_ResultsInOneSendAndOneReservation()
    {
        // Arrange — In-memory relational SQLite database enforcing real table UNIQUE constraints
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var setupContext = new ApplicationDbContext(options))
        {
            setupContext.Database.EnsureCreated();
        }

        // Two distinct ApplicationDbContext instances and two distinct NotificationOrchestrator instances
        // (Simulating two independent backend servers / worker processes)
        using var context1 = new ApplicationDbContext(options);
        using var context2 = new ApplicationDbContext(options);

        var sharedEmailService = new MockEmailService("success");
        var orchestrator1 = new NotificationOrchestrator(
            sharedEmailService,
            new NotificationLogRepository(context1),
            NullLogger<NotificationOrchestrator>.Instance);

        var orchestrator2 = new NotificationOrchestrator(
            sharedEmailService,
            new NotificationLogRepository(context2),
            NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:cross-instance-submitted";

        // Act — Fire simultaneous notifications from separate instances
        var task1 = orchestrator1.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-CROSS-01");

        var task2 = orchestrator2.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-CROSS-01");

        var results = await Task.WhenAll(task1, task2);

        // Assert — Both report true (no 500, duplicate handled safely)
        Assert.True(results[0]);
        Assert.True(results[1]);

        // Email service dispatched exactly ONE email across both server instances
        Assert.Single(sharedEmailService.GetSentMessages());

        // Database has exactly ONE log entry with status Sent and SentAt populated
        using var verifyContext = new ApplicationDbContext(options);
        var logs = await verifyContext.NotificationLogs
            .Where(n => n.NotificationKey == notificationKey)
            .ToListAsync();

        Assert.Single(logs);
        Assert.Equal(NotificationStatus.Sent, logs[0].Status);
        Assert.True(logs[0].Success);
        Assert.NotNull(logs[0].SentAt);
    }

    [Fact]
    public async Task DuplicateRequests_HandledByDifferentOrchestratorInstances_DoNotCauseDuplicateDelivery()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var setupContext = new ApplicationDbContext(options))
        {
            setupContext.Database.EnsureCreated();
        }

        using var contextA = new ApplicationDbContext(options);
        using var contextB = new ApplicationDbContext(options);

        var sharedEmailService = new MockEmailService("success");
        var orchestratorA = new NotificationOrchestrator(
            sharedEmailService, new NotificationLogRepository(contextA), NullLogger<NotificationOrchestrator>.Instance);
        var orchestratorB = new NotificationOrchestrator(
            sharedEmailService, new NotificationLogRepository(contextB), NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:sequential-cross-instance";

        // Act 1: Instance A processes first
        var resultA = await orchestratorA.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-INST-01");

        Assert.True(resultA);
        Assert.Single(sharedEmailService.GetSentMessages());

        // Act 2: Instance B processes duplicate request later
        var resultB = await orchestratorB.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-INST-01");

        Assert.True(resultB);
        // Still only 1 email sent!
        Assert.Single(sharedEmailService.GetSentMessages());
    }

    [Fact]
    public async Task NewDocumentVerificationAttempt_UsingUnchangedDocuments_GeneratesNewLegitimateNotification()
    {
        // Arrange — ClaimRepository and ClaimService
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var claimService = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var userId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = userId,
            ClaimNumber = "CLM-UNCHANGED-DOCS",
            ClaimType = ClaimType.Motor,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Description = "Accident claim with 4 documents",
            ClaimedAmount = 50000m,
            Status = ClaimStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Documents = new List<ClaimDocument>
            {
                new() { Id = Guid.NewGuid(), DocumentType = "RepairEstimate", FileName = "doc1.pdf", FileUrl = "/doc1.pdf", ContentType = "application/pdf", FileSize = 1024, VerificationStatus = DocumentVerificationStatus.Verified },
                new() { Id = Guid.NewGuid(), DocumentType = "PoliceReport", FileName = "doc2.pdf", FileUrl = "/doc2.pdf", ContentType = "application/pdf", FileSize = 2048, VerificationStatus = DocumentVerificationStatus.Verified },
                new() { Id = Guid.NewGuid(), DocumentType = "DrivingLicense", FileName = "doc3.pdf", FileUrl = "/doc3.pdf", ContentType = "application/pdf", FileSize = 3072, VerificationStatus = DocumentVerificationStatus.Verified },
                new() { Id = Guid.NewGuid(), DocumentType = "NationalID", FileName = "doc4.pdf", FileUrl = "/doc4.pdf", ContentType = "application/pdf", FileSize = 4096, VerificationStatus = DocumentVerificationStatus.Verified }
            }
        };
        await claimRepo.AddAsync(claim);

        // Verification Attempt 1: First verification cycle
        var res1 = await claimService.VerifyDocumentsAsync(claim.Id, userId, Role.ClaimsAdjuster, idempotencyKey: "run-cycle-1");
        Assert.NotNull(res1.AttemptId);

        var keyAttempt1 = DocumentNotificationKeys.ForDocumentsVerified(
            claim.Id, claim.Documents, occurrenceId: res1.AttemptId?.ToString());

        var send1 = await _orchestrator.NotifyAsync(
            keyAttempt1, userId, "user@example.com", claim.Id,
            NotificationType.DocumentsVerified, claim.ClaimNumber);

        Assert.True(send1);
        Assert.Single(_emailService.GetSentMessages());

        // Verification Attempt 2: Deliberately new verification of the SAME 4 unchanged documents
        var res2 = await claimService.VerifyDocumentsAsync(claim.Id, userId, Role.ClaimsAdjuster, idempotencyKey: "run-cycle-2");
        Assert.NotNull(res2.AttemptId);
        Assert.NotEqual(res1.AttemptId, res2.AttemptId);

        var keyAttempt2 = DocumentNotificationKeys.ForDocumentsVerified(
            claim.Id, claim.Documents, occurrenceId: res2.AttemptId?.ToString());

        Assert.NotEqual(keyAttempt1, keyAttempt2);

        var send2 = await _orchestrator.NotifyAsync(
            keyAttempt2, userId, "user@example.com", claim.Id,
            NotificationType.DocumentsVerified, claim.ClaimNumber);

        Assert.True(send2);
        // Both legitimate attempts resulted in notification delivery
        Assert.Equal(2, _emailService.GetSentMessages().Count);
    }

    [Fact]
    public async Task RetryingSameVerificationAttempt_DoesNotGenerateAnotherNotification()
    {
        // Arrange
        var claimRepo = new FakeClaimRepository();
        var storage = new FakeDocumentStorageService();
        var policyVal = new FakePolicyValidationService();
        var aiClient = new FakeDocumentVerificationClient();
        var claimService = new ClaimService(claimRepo, storage, policyVal, aiClient);

        var userId = Guid.NewGuid();
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            PolicyHolderId = userId,
            ClaimNumber = "CLM-RETRY-DOCS",
            ClaimType = ClaimType.Motor,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Colombo",
            Description = "Accident claim",
            ClaimedAmount = 50000m,
            Status = ClaimStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Documents = new List<ClaimDocument>
            {
                new() { Id = Guid.NewGuid(), DocumentType = "RepairEstimate", FileName = "doc1.pdf", FileUrl = "/doc1.pdf", ContentType = "application/pdf", FileSize = 1024, VerificationStatus = DocumentVerificationStatus.Verified }
            }
        };
        await claimRepo.AddAsync(claim);

        // Run 1: Initial call with Idempotency-Key
        var run1 = await claimService.VerifyDocumentsAsync(claim.Id, userId, Role.ClaimsAdjuster, idempotencyKey: "http-retry-key-999");
        Assert.NotNull(run1.AttemptId);

        var key1 = DocumentNotificationKeys.ForDocumentsVerified(
            claim.Id, claim.Documents, occurrenceId: run1.AttemptId.Value.ToString());

        var res1 = await _orchestrator.NotifyAsync(
            key1, userId, "user@example.com", claim.Id,
            NotificationType.DocumentsVerified, claim.ClaimNumber);

        Assert.True(res1);
        Assert.Single(_emailService.GetSentMessages());

        // Run 2: Exact HTTP retry reusing same Idempotency-Key
        var run2 = await claimService.VerifyDocumentsAsync(claim.Id, userId, Role.ClaimsAdjuster, idempotencyKey: "http-retry-key-999");
        Assert.Equal(run1.AttemptId, run2.AttemptId);

        var key2 = DocumentNotificationKeys.ForDocumentsVerified(
            claim.Id, claim.Documents, occurrenceId: run2.AttemptId?.ToString());

        Assert.Equal(key1, key2);

        var res2 = await _orchestrator.NotifyAsync(
            key2, userId, "user@example.com", claim.Id,
            NotificationType.DocumentsVerified, claim.ClaimNumber);

        // Handled gracefully via idempotency — no duplicate email sent
        Assert.True(res2);
        Assert.Single(_emailService.GetSentMessages());
    }

    [Fact]
    public async Task UnexpectedTermination_StrandedProcessingLease_SafelyRecoveredAfterTimeout()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:stranded-crash";

        // Simulate crash: notification was reserved in Processing status 10 minutes ago, but process stopped before send
        var strandedLog = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClaimId = claimId,
            NotificationKey = notificationKey,
            NotificationType = NotificationType.ClaimSubmitted,
            Channel = NotificationChannel.Email,
            Recipient = "user@example.com",
            Subject = "Claim Submitted",
            Body = "Body",
            Status = NotificationStatus.Processing,
            Success = false,
            Provider = "Mock",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
            SentAt = null
        };
        _context.NotificationLogs.Add(strandedLog);
        await _context.SaveChangesAsync();

        // Act — Backend restarted, retry comes in
        var result = await _orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-CRASH-01");

        // Assert — Lease safely reclaimed and delivered
        Assert.True(result);
        Assert.Single(_emailService.GetSentMessages());

        var updatedLog = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        Assert.NotNull(updatedLog);
        Assert.Equal(NotificationStatus.Sent, updatedLog.Status);
        Assert.True(updatedLog.Success);
        Assert.NotNull(updatedLog.SentAt);
        // Original CreatedAt preserved from 10 minutes ago
        Assert.Equal(strandedLog.CreatedAt, updatedLog.CreatedAt);
    }

    [Fact]
    public async Task ActiveProcessingLease_PreventsConcurrentDispatch()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:active-lease";

        // Active reservation (< 5 minutes old) currently being processed by another worker
        var activeLog = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClaimId = claimId,
            NotificationKey = notificationKey,
            NotificationType = NotificationType.ClaimSubmitted,
            Channel = NotificationChannel.Email,
            Recipient = "user@example.com",
            Subject = "Claim Submitted",
            Body = "Body",
            Status = NotificationStatus.Processing,
            Success = false,
            Provider = "Mock",
            CreatedAt = DateTime.UtcNow.AddSeconds(-30),
            UpdatedAt = DateTime.UtcNow.AddSeconds(-30),
            SentAt = null
        };
        _context.NotificationLogs.Add(activeLog);
        await _context.SaveChangesAsync();

        // Act — Another request tries to dispatch
        var result = await _orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-ACTIVE-01");

        // Assert — Treated as handled, no second email sent
        Assert.True(result);
        Assert.Empty(_emailService.GetSentMessages());
    }

    [Fact]
    public async Task MockEmailServiceFailure_DoesNotRollBackBusinessOperation()
    {
        // Arrange — Email service that reports failure
        var failingEmailService = new MockEmailService("failure");
        var orchestrator = new NotificationOrchestrator(
            failingEmailService,
            _logRepository,
            NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var notificationKey = $"claim:{claimId}:fail-isolation";

        // Act — Notification attempt fails
        var result = await orchestrator.NotifyAsync(
            notificationKey, userId, "user@example.com", claimId,
            NotificationType.ClaimSubmitted, "CLM-FAIL-ISO");

        // Assert — Returns false, does NOT throw exception
        Assert.False(result);

        // Audit log reflects failure
        var log = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        Assert.NotNull(log);
        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.False(log.Success);
        Assert.Null(log.SentAt);
    }

    /// <summary>
    /// Test double simulating Postgres unique violation (23505).
    /// </summary>
    private class PostgresUniqueViolationMockRepository : INotificationLogRepository
    {
        public int AddCallCount { get; private set; }

        public Task<NotificationLog> AddAsync(NotificationLog log)
        {
            AddCallCount++;
            // Simulate Postgres 23505 unique constraint violation
            throw new DuplicateNotificationException(
                "duplicate key value violates unique constraint \"IX_NotificationLogs_NotificationKey\" (23505)");
        }

        public Task<bool> ExistsByKeyAsync(string notificationKey) => Task.FromResult(false);
        public Task<bool> ExistsAsync(Guid userId, Guid? claimId, NotificationType type) => Task.FromResult(false);
        public Task<List<NotificationLog>> GetByUserIdAsync(Guid userId) => Task.FromResult(new List<NotificationLog>());
        public Task<List<NotificationLog>> GetByClaimIdAsync(Guid claimId) => Task.FromResult(new List<NotificationLog>());
        public Task<List<NotificationLog>> GetByPayoutIdAsync(Guid payoutId) => Task.FromResult(new List<NotificationLog>());
        public Task<List<NotificationLog>> GetAllAsync(int page = 1, int pageSize = 50) => Task.FromResult(new List<NotificationLog>());

        public Task<NotificationReservationResult> TryReserveAsync(NotificationLog draftLog, TimeSpan? leaseDuration = null)
        {
            AddCallCount++;
            throw new DuplicateNotificationException(
                "duplicate key value violates unique constraint \"IX_NotificationLogs_NotificationKey\" (23505)");
        }

        public Task<NotificationLog> MarkSentAsync(string notificationKey, string provider, string? providerMessageId) =>
            throw new NotImplementedException();

        public Task<NotificationLog> MarkAcceptedAsync(string notificationKey, string provider, string? providerMessageId) =>
            throw new NotImplementedException();

        public Task<NotificationLog> MarkFailedAsync(string notificationKey, string provider, string? errorMessage) =>
            throw new NotImplementedException();
    }
}
