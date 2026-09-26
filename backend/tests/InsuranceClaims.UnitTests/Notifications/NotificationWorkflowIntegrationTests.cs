using System.Security.Claims;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Services;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.Notifications.Services;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.ExternalServices;
using InsuranceClaims.Infrastructure.ExternalServices.Email;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Repositories;
using InsuranceClaims.UnitTests.ClaimsManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainClaim = InsuranceClaims.Domain.ClaimsManagement.Claim;
using SecurityClaim = System.Security.Claims.Claim;

namespace InsuranceClaims.UnitTests.Notifications;

/// <summary>
/// Integration tests verifying:
/// 1. MockEmailService = failure does NOT prevent business operations (Claim submission, Payout approval).
/// 2. NotificationLog persists with Success = false and appropriate error message.
/// 3. NotificationsController enforces role authorization and prevents IDOR / cross-policyholder leakage.
/// </summary>
public class NotificationWorkflowIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ClaimRepository _claimRepository;
    private readonly PayoutRepository _payoutRepository;
    private readonly NotificationLogRepository _logRepository;
    private readonly UserEmailResolver _userEmailResolver;

    private readonly Guid _policyholderAId = Guid.NewGuid();
    private readonly Guid _policyholderBId = Guid.NewGuid();
    private readonly Guid _underwriterId = Guid.NewGuid();

    private Policy _policyA = null!;
    private DomainClaim _claimA = null!;

    public NotificationWorkflowIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"NotifIntegrationDb_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _claimRepository = new ClaimRepository(_context);
        _payoutRepository = new PayoutRepository(_context);
        _logRepository = new NotificationLogRepository(_context);
        _userEmailResolver = new UserEmailResolver(_context);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        // Seed users
        var userA = new User
        {
            Id = _policyholderAId,
            Email = "policyholderA@example.com",
            Role = Role.Policyholder,
            FirstName = "Alice",
            LastName = "Policyholder",
            CreatedAt = DateTime.UtcNow
        };
        var userB = new User
        {
            Id = _policyholderBId,
            Email = "policyholderB@example.com",
            Role = Role.Policyholder,
            FirstName = "Bob",
            LastName = "Policyholder",
            CreatedAt = DateTime.UtcNow
        };
        var underwriter = new User
        {
            Id = _underwriterId,
            Email = "underwriter@example.com",
            Role = Role.Underwriter,
            FirstName = "Ursula",
            LastName = "Underwriter",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(userA, userB, underwriter);

        // Seed policy
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Auto policy",
            BasePremiumRate = 500m
        };
        _context.PolicyTypes.Add(policyType);

        _policyA = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-INT-100",
            PolicyholderId = _policyholderAId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 50000m,
            Premium = 600m,
            Deductible = 500m,
            StartDate = DateTime.UtcNow.AddMonths(-1),
            ExpiryDate = DateTime.UtcNow.AddMonths(11),
            Status = PolicyStatus.Active
        };
        _context.Policies.Add(_policyA);

        // Seed Draft Claim for Policyholder A
        _claimA = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-INT-100",
            PolicyId = _policyA.Id,
            Policy = _policyA,
            PolicyHolderId = _policyholderAId,
            ClaimType = ClaimType.Auto,
            ClaimedAmount = 5000m,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            IncidentLocation = "Highway 1",
            Description = "Front collision",
            Status = ClaimStatus.Draft
        };
        _context.Claims.Add(_claimA);

        _context.SaveChanges();
    }

    private void SetUserContext(ControllerBase controller, Guid userId, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()),
            new SecurityClaim(ClaimTypes.Role, role)
        }, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task ClaimSubmission_WhenMockEmailFails_ClaimSucceeds_AndNotificationLogIsFailed()
    {
        // Arrange — MockEmailService configured to FAIL
        var failingEmailService = new MockEmailService("failure");
        var orchestrator = new NotificationOrchestrator(
            failingEmailService, _logRepository, NullLogger<NotificationOrchestrator>.Instance);

        var policyValidation = new PolicyValidationService(_context);
        var claimService = new ClaimService(
            _claimRepository, new FakeDocumentStorageService(), policyValidation, new FakeDocumentVerificationClient());

        var controller = new ClaimsController(
            claimService, orchestrator, _userEmailResolver, NullLogger<ClaimsController>.Instance);
        SetUserContext(controller, _policyholderAId, "Policyholder");

        // Act — submit claim
        var response = await controller.SubmitClaim(_claimA.Id);

        // Assert 1: Claim submission STILL SUCCEEDED (non-authoritative notification)
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var claimDto = Assert.IsType<ClaimResponseDto>(okResult.Value);
        Assert.Equal(ClaimStatus.Submitted.ToString(), claimDto.Status);

        // Verify DB claim status is Submitted
        var updatedClaim = await _context.Claims.FindAsync(_claimA.Id);
        Assert.NotNull(updatedClaim);
        Assert.Equal(ClaimStatus.Submitted, updatedClaim.Status);

        // Assert 2: NotificationLog was persisted and marked as Failed
        var logs = await _context.NotificationLogs.Where(l => l.ClaimId == _claimA.Id).ToListAsync();
        Assert.Single(logs);
        var log = logs[0];
        Assert.False(log.Success, "NotificationLog should be marked as failed");
        Assert.NotNull(log.ErrorMessage);
        Assert.Contains("Simulated", log.ErrorMessage);
        Assert.Equal("Mock", log.Provider);
        Assert.Equal($"claim:{_claimA.Id}:submitted", log.NotificationKey);
        Assert.Null(log.SentAt);
    }

    [Fact]
    public async Task ClaimSubmission_Success_RecordsNotificationLog_AppearsInHistory_AndPreventsDuplicates()
    {
        // Arrange — MockEmailService configured for success
        var mockEmail = new MockEmailService("success");
        var orchestrator = new NotificationOrchestrator(
            mockEmail, _logRepository, NullLogger<NotificationOrchestrator>.Instance);

        var policyValidation = new PolicyValidationService(_context);
        var claimService = new ClaimService(
            _claimRepository, new FakeDocumentStorageService(), policyValidation, new FakeDocumentVerificationClient());

        var claimsController = new ClaimsController(
            claimService, orchestrator, _userEmailResolver, NullLogger<ClaimsController>.Instance);
        SetUserContext(claimsController, _policyholderAId, "Policyholder");

        // Seed an isolated new draft claim
        var newClaim = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-SUCCESS-001",
            PolicyId = _policyA.Id,
            Policy = _policyA,
            PolicyHolderId = _policyholderAId,
            ClaimType = ClaimType.Auto,
            ClaimedAmount = 1500m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "Parkway 5",
            Description = "Minor dent",
            Status = ClaimStatus.Draft
        };
        _context.Claims.Add(newClaim);
        await _context.SaveChangesAsync();

        // Act 1 — Submit claim
        var response = await claimsController.SubmitClaim(newClaim.Id);

        // Assert 1 — Claim remains Submitted
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var claimDto = Assert.IsType<ClaimResponseDto>(okResult.Value);
        Assert.Equal(ClaimStatus.Submitted.ToString(), claimDto.Status);

        var dbClaim = await _context.Claims.FindAsync(newClaim.Id);
        Assert.NotNull(dbClaim);
        Assert.Equal(ClaimStatus.Submitted, dbClaim.Status);

        // Assert 2 — One ClaimSubmitted notification recorded with authoritative email & delivery success
        var logs = await _context.NotificationLogs.Where(l => l.ClaimId == newClaim.Id).ToListAsync();
        Assert.Single(logs);
        var log = logs[0];
        Assert.True(log.Success);
        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.Equal("policyholderA@example.com", log.Recipient);
        Assert.Equal(NotificationType.ClaimSubmitted, log.NotificationType);
        Assert.Equal($"claim:{newClaim.Id}:submitted", log.NotificationKey);
        Assert.NotNull(log.SentAt);

        // Assert 3 — Policyholder sees notification in Notification History
        var notifController = new NotificationsController(_logRepository, _claimRepository, _payoutRepository);
        SetUserContext(notifController, _policyholderAId, "Policyholder");
        var historyResult = await notifController.GetNotifications();
        var historyOk = Assert.IsType<OkObjectResult>(historyResult.Result);
        var historyList = Assert.IsType<List<NotificationLogDto>>(historyOk.Value);
        Assert.Contains(historyList, item => item.ClaimId == newClaim.Id && item.Success);

        // Act 2 — Repeated submission event / duplicate trigger
        await orchestrator.NotifyAsync(
            $"claim:{newClaim.Id}:submitted",
            _policyholderAId,
            "policyholderA@example.com",
            newClaim.Id,
            NotificationType.ClaimSubmitted,
            newClaim.ClaimNumber);

        // Assert 4 — Duplicate prevented, still exactly one notification log
        var logsAfterDuplicate = await _context.NotificationLogs.Where(l => l.ClaimId == newClaim.Id).ToListAsync();
        Assert.Single(logsAfterDuplicate);
    }

    [Fact]
    public async Task PayoutApproval_WhenMockEmailFails_PayoutApprovalSucceeds_AndNotificationLogIsFailed()
    {
        // Arrange — Claim is approved, Payout is in Draft/PendingApproval
        _claimA.Status = ClaimStatus.Approved;
        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = _claimA.Id,
            Claim = _claimA,
            ApprovedClaimAmount = 4500m,
            CoverageLimit = 50000m,
            Deductible = 500m,
            Status = PayoutStatus.PendingApproval
        };
        payout.CalculatePayout();
        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();

        var failingEmailService = new MockEmailService("failure");
        var orchestrator = new NotificationOrchestrator(
            failingEmailService, _logRepository, NullLogger<NotificationOrchestrator>.Instance);

        var payoutService = new PayoutService(
            _payoutRepository, new StubPayoutContextProvider(_claimA, _policyA), new StubValidationAgentGateway());

        var controller = new PayoutsController(
            payoutService,
            new FakePaymentGateway(),
            _payoutRepository,
            new PaymentTransactionRepository(_context),
            NullLogger<PayoutsController>.Instance,
            orchestrator,
            _userEmailResolver);

        SetUserContext(controller, _underwriterId, "Underwriter");

        // Act — approve payout
        var response = await controller.ApprovePayout(
            payout.Id, new PayoutApprovalRequestDto { Comments = "Approved by senior underwriter" });

        // Assert 1: Payout approval STILL SUCCEEDED (non-authoritative notification)
        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        var payoutDto = Assert.IsType<PayoutDto>(okResult.Value);
        Assert.Equal(PayoutStatus.Approved, payoutDto.Status);

        // Verify DB payout status is Approved
        var updatedPayout = await _context.Payouts.FindAsync(payout.Id);
        Assert.NotNull(updatedPayout);
        Assert.Equal(PayoutStatus.Approved, updatedPayout.Status);

        // Assert 2: NotificationLog was persisted and marked as Failed
        var logs = await _context.NotificationLogs.Where(l => l.PayoutId == payout.Id).ToListAsync();
        Assert.Single(logs);
        var log = logs[0];
        Assert.False(log.Success, "NotificationLog should be marked as failed");
        Assert.NotNull(log.ErrorMessage);
        Assert.Contains("Simulated", log.ErrorMessage);
        Assert.Equal(payout.Id, log.PayoutId);
        Assert.Equal($"payout:{payout.Id}:approved", log.NotificationKey);
        Assert.Null(log.SentAt);
    }

    [Fact]
    public async Task NotificationsController_PolicyholderCannotAccessOtherUserNotifications_PreventsIdor()
    {
        // Arrange — seed notifications for User A and User B
        var logA = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = _policyholderAId,
            ClaimId = _claimA.Id,
            NotificationKey = $"claim:{_claimA.Id}:submitted",
            NotificationType = NotificationType.ClaimSubmitted,
            Recipient = "policyholderA@example.com",
            Subject = "Claim Submitted A",
            Body = "<p>User A</p>",
            Success = true,
            Provider = "Mock",
            SentAt = DateTime.UtcNow
        };

        var logB = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = _policyholderBId,
            ClaimId = Guid.NewGuid(),
            NotificationKey = "claim:other:submitted",
            NotificationType = NotificationType.ClaimSubmitted,
            Recipient = "policyholderB@example.com",
            Subject = "Claim Submitted B",
            Body = "<p>User B</p>",
            Success = true,
            Provider = "Mock",
            SentAt = DateTime.UtcNow
        };

        _context.NotificationLogs.AddRange(logA, logB);
        await _context.SaveChangesAsync();

        var controller = new NotificationsController(_logRepository, _claimRepository, _payoutRepository);

        // Act — Policyholder A requests notifications passing userId = Policyholder B (IDOR attempt)
        SetUserContext(controller, _policyholderAId, "Policyholder");
        var result = await controller.GetNotifications(userId: _policyholderBId);

        // Assert — Returned notifications ONLY contain User A's logs, ignoring the spoofed userId query param
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtoList = Assert.IsType<List<NotificationLogDto>>(okResult.Value);
        Assert.Single(dtoList);
        Assert.Equal(_policyholderAId, dtoList[0].UserId);
        Assert.DoesNotContain(dtoList, d => d.UserId == _policyholderBId);
    }

    [Fact]
    public async Task NotificationsController_Policyholder_CannotAccessOtherUserClaimNotifications_ReturnsForbidden()
    {
        // Arrange — Policyholder B owns a different claim
        var claimB = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = "CLM-B-200",
            PolicyId = _policyA.Id,
            PolicyHolderId = _policyholderBId,
            ClaimType = ClaimType.Auto,
            ClaimedAmount = 2000m,
            IncidentDate = DateTime.UtcNow.AddDays(-2),
            IncidentLocation = "City center",
            Description = "Fender bender",
            Status = ClaimStatus.Submitted
        };
        _context.Claims.Add(claimB);
        await _context.SaveChangesAsync();

        var controller = new NotificationsController(_logRepository, _claimRepository, _payoutRepository);

        // Act — Policyholder A requests notifications for Claim B (owned by Bob)
        SetUserContext(controller, _policyholderAId, "Policyholder");
        var result = await controller.GetByClaimId(claimB.Id);

        // Assert — 403 Forbidden (cross-policyholder IDOR prevented)
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task NotificationsController_DatabaseException_ReturnsSanitized500WithoutLeakingDetails()
    {
        // Arrange — Simulate database error (such as missing relation or connection error)
        var failingRepo = new FailingNotificationLogRepository();
        var controller = new NotificationsController(failingRepo, _claimRepository, _payoutRepository);
        SetUserContext(controller, _policyholderAId, "Policyholder");

        // Act
        var result = await controller.GetNotifications();

        // Assert — Returns HTTP 500 with sanitized message and no raw SQL/table errors
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        Assert.Contains("Unable to load notifications. Please try again.", json);
        Assert.DoesNotContain("relation \\\"NotificationLogs\\\" does not exist", json);
    }

    private class FailingNotificationLogRepository : INotificationLogRepository
    {
        public Task<NotificationLog> AddAsync(NotificationLog log) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<List<NotificationLog>> GetByUserIdAsync(Guid userId) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<List<NotificationLog>> GetByClaimIdAsync(Guid claimId) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<List<NotificationLog>> GetByPayoutIdAsync(Guid payoutId) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<bool> ExistsByKeyAsync(string notificationKey) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<bool> ExistsAsync(Guid userId, Guid? claimId, NotificationType type) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<List<NotificationLog>> GetAllAsync(int page = 1, int pageSize = 50) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<NotificationReservationResult> TryReserveAsync(NotificationLog draftLog, TimeSpan? leaseDuration = null) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<NotificationLog> MarkSentAsync(string notificationKey, string provider, string? providerMessageId) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<NotificationLog> MarkAcceptedAsync(string notificationKey, string provider, string? providerMessageId) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
        public Task<NotificationLog> MarkFailedAsync(string notificationKey, string provider, string? errorMessage) => throw new InvalidOperationException("relation \"NotificationLogs\" does not exist");
    }

    // ───── Test Doubles ──────────────────────────────────────────────

    private class StubPayoutContextProvider : IPayoutContextProvider
    {
        private readonly DomainClaim _claim;
        private readonly Policy _policy;

        public StubPayoutContextProvider(DomainClaim claim, Policy policy)
        {
            _claim = claim;
            _policy = policy;
        }

        public Task<PayoutContext?> GetPayoutContextAsync(Guid claimId)
        {
            return Task.FromResult<PayoutContext?>(new PayoutContext
            {
                ClaimId = _claim.Id,
                PolicyId = _policy.Id,
                ApprovedClaimAmount = _claim.ClaimedAmount,
                CoverageLimit = _policy.CoverageLimit,
                Deductible = _policy.Deductible,
                PolicyType = _policy.PolicyType?.Name ?? "Comprehensive Auto",
                ClaimType = _claim.ClaimType.ToString()
            });
        }
    }

    private class StubValidationAgentGateway : IPayoutValidationAgentGateway
    {
        public Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request)
        {
            return Task.FromResult(new PayoutValidationResult
            {
                Valid = true,
                RequiresHumanApproval = true
            });
        }
    }

    private class FakePaymentGateway : IPaymentGateway
    {
        public Task<PaymentGatewayResult> CreatePayoutAsync(PaymentGatewayRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentGatewayResult { Success = true, Provider = "Mock", ProviderStatus = "succeeded" });

        public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(string providerTransactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentGatewayStatusResult?>(new PaymentGatewayStatusResult
            {
                ProviderTransactionId = providerTransactionId,
                Status = "succeeded",
                UpdatedAt = DateTime.UtcNow
            });

        public Task<bool> ValidateWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
