using System.Text.Json;
using InsuranceClaims.Api.Controllers;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.RiskAssessment.DTOs;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.RiskAssessment.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.RiskAssessment;

public class RiskAssessmentsControllerTests
{
    private readonly StubRiskAssessmentService _riskService = new();
    private readonly StubClaimRepo _claimRepo = new();
    private readonly StubNotificationOrchestrator _notificationOrchestrator = new();
    private readonly StubUserEmailResolver _emailResolver = new();

    private RiskAssessmentsController CreateController()
    {
        return new RiskAssessmentsController(
            _riskService,
            _claimRepo,
            _notificationOrchestrator,
            _emailResolver,
            NullLogger<RiskAssessmentsController>.Instance);
    }

    [Fact]
    public async Task AssessClaim_WhenEscalate_TriggersRiskAssessmentNeedsReviewNotification()
    {
        var controller = CreateController();
        var claimId = Guid.NewGuid();
        var policyHolderId = Guid.NewGuid();

        _claimRepo.Claims[claimId] = new Claim
        {
            Id = claimId,
            PolicyHolderId = policyHolderId,
            ClaimNumber = "CLM-2026-TEST"
        };
        _emailResolver.Emails[policyHolderId] = "policyholder@test.com";

        _riskService.AssessResult = new RiskAssessmentDto
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            ClaimNumber = "CLM-2026-TEST",
            RiskScore = 85m,
            RiskLevel = RiskLevel.Critical,
            Recommendation = RiskRecommendation.Escalate,
            FraudFlagCount = 3
        };

        var response = await controller.AssessClaim(claimId, new AssessClaimRequest { IncludeAiAnalysis = true });

        var okResult = Assert.IsType<OkObjectResult>(response);
        var dto = Assert.IsType<RiskAssessmentDto>(okResult.Value);
        Assert.Equal(RiskRecommendation.Escalate, dto.Recommendation);

        // Verify notification was sent
        Assert.Single(_notificationOrchestrator.SentNotifications);
        var sent = _notificationOrchestrator.SentNotifications[0];
        Assert.Equal(NotificationType.RiskAssessmentNeedsReview, sent.Type);
        Assert.Equal(policyHolderId, sent.UserId);
        Assert.Equal("policyholder@test.com", sent.Email);
        Assert.Equal($"claim:{claimId}:risk-needs-review:{dto.Id}", sent.Key);
    }

    [Fact]
    public async Task Escalate_WithJsonStringPriority_DeserializesAndCreatesFraudCase()
    {
        var controller = CreateController();
        var assessmentId = Guid.NewGuid();

        // Simulate incoming JSON payload from frontend: {"reason":"Suspicious duplicate","priority":"High"}
        var json = "{\"reason\":\"Suspicious duplicate\",\"priority\":\"High\"}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<EscalateRequest>(json, options);

        Assert.NotNull(request);
        Assert.Equal("Suspicious duplicate", request.Reason);
        Assert.Equal(FraudCasePriority.High, request.Priority);

        _riskService.EscalateResult = new FraudCaseDto
        {
            Id = Guid.NewGuid(),
            RiskAssessmentId = assessmentId,
            Priority = request.Priority,
            Status = FraudCaseStatus.Open
        };

        var response = await controller.Escalate(assessmentId, request);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var caseDto = Assert.IsType<FraudCaseDto>(okResult.Value);
        Assert.Equal(FraudCasePriority.High, caseDto.Priority);
    }

    [Fact]
    public async Task GetAllAssessments_ReturnsOkWithAssessments()
    {
        var controller = CreateController();
        _riskService.AllAssessments = new List<RiskAssessmentDto>
        {
            new() { Id = Guid.NewGuid(), RiskScore = 75m, Recommendation = RiskRecommendation.Escalate }
        };

        var response = await controller.GetAllAssessments();

        var okResult = Assert.IsType<OkObjectResult>(response);
        var items = Assert.IsAssignableFrom<IReadOnlyList<RiskAssessmentDto>>(okResult.Value);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetFlaggedClaims_ReturnsOkWithFlaggedClaims()
    {
        var controller = CreateController();
        _riskService.FlaggedClaims = new List<RiskAssessmentDto>
        {
            new() { Id = Guid.NewGuid(), RiskScore = 80m, FraudFlagCount = 2, Recommendation = RiskRecommendation.Escalate }
        };

        var response = await controller.GetFlaggedClaims();

        var okResult = Assert.IsType<OkObjectResult>(response);
        var items = Assert.IsAssignableFrom<IReadOnlyList<RiskAssessmentDto>>(okResult.Value);
        Assert.Single(items);
    }

    // ── Stubs ─────────────────────────────────────────────────────────────

    private class StubRiskAssessmentService : IRiskAssessmentService
    {
        public RiskAssessmentDto? AssessResult { get; set; }
        public FraudCaseDto? EscalateResult { get; set; }
        public List<RiskAssessmentDto> AllAssessments { get; set; } = new();
        public List<RiskAssessmentDto> FlaggedClaims { get; set; } = new();

        public Task<RiskAssessmentDto> AssessClaimAsync(Guid claimId, AssessClaimRequest request)
            => Task.FromResult(AssessResult ?? new RiskAssessmentDto { ClaimId = claimId });

        public Task<RiskAssessmentDto?> GetAssessmentAsync(Guid claimId) => Task.FromResult<RiskAssessmentDto?>(null);
        public Task<IReadOnlyList<RiskAssessmentDto>> GetFlaggedClaimsAsync() => Task.FromResult<IReadOnlyList<RiskAssessmentDto>>(FlaggedClaims);
        public Task<IReadOnlyList<RiskAssessmentDto>> GetAllAssessmentsAsync() => Task.FromResult<IReadOnlyList<RiskAssessmentDto>>(AllAssessments);
        public Task<IReadOnlyList<FraudCaseDto>> GetHistoryAsync(Guid policyholderId) => Task.FromResult<IReadOnlyList<FraudCaseDto>>(new List<FraudCaseDto>());
        public Task<FraudCaseDto> EscalateAsync(Guid assessmentId, EscalateRequest request) => Task.FromResult(EscalateResult ?? new FraudCaseDto());
        public Task<IReadOnlyList<FraudFlagDto>> GetFlagsAsync(Guid claimId) => Task.FromResult<IReadOnlyList<FraudFlagDto>>(new List<FraudFlagDto>());
        public Task<FraudCaseDto> UpdateFraudCaseAsync(Guid fraudCaseId, UpdateFraudCaseRequest request) => Task.FromResult(new FraudCaseDto());
        public Task<PolicyholderReviewStatusDto> GetPolicyholderStatusAsync(Guid claimId) => Task.FromResult(new PolicyholderReviewStatusDto());
    }

    private class StubClaimRepo : IClaimRepository
    {
        public Dictionary<Guid, Claim> Claims { get; } = new();

        public Task<Claim?> GetByIdAsync(Guid id)
            => Task.FromResult(Claims.TryGetValue(id, out var claim) ? claim : null);

        public Task<Claim?> GetByIdWithDocumentsAsync(Guid id)
            => Task.FromResult(Claims.TryGetValue(id, out var claim) ? claim : null);

        public Task<List<Claim>> GetByPolicyHolderIdAsync(Guid policyHolderId)
            => Task.FromResult(Claims.Values.Where(c => c.PolicyHolderId == policyHolderId).ToList());

        public Task<List<Claim>> GetAllAsync(string? statusFilter = null, string? searchTerm = null, Guid? policyHolderId = null)
            => Task.FromResult(Claims.Values.ToList());

        public Task<Claim> AddAsync(Claim claim)
        {
            Claims[claim.Id] = claim;
            return Task.FromResult(claim);
        }

        public Task<Claim> UpdateAsync(Claim claim)
        {
            Claims[claim.Id] = claim;
            return Task.FromResult(claim);
        }

        public Task DeleteAsync(Claim claim)
        {
            Claims.Remove(claim.Id);
            return Task.CompletedTask;
        }

        public Task DeleteDocumentAsync(ClaimDocument document)
            => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid id)
            => Task.FromResult(Claims.ContainsKey(id));

        public Task<string> GenerateClaimNumberAsync()
            => Task.FromResult("CLM-1");

        public Task<InsuranceClaims.Domain.AgentWorkflows.AgentWorkflow?> GetWorkflowAttemptByIdempotencyKeyAsync(Guid claimId, string idempotencyKey)
            => Task.FromResult<InsuranceClaims.Domain.AgentWorkflows.AgentWorkflow?>(null);

        public Task<InsuranceClaims.Domain.AgentWorkflows.AgentWorkflow> RecordWorkflowAttemptAsync(InsuranceClaims.Domain.AgentWorkflows.AgentWorkflow workflow)
            => Task.FromResult(workflow);
    }

    private class StubNotificationOrchestrator : INotificationOrchestrator
    {
        public List<(string Key, Guid UserId, string Email, NotificationType Type)> SentNotifications { get; } = new();

        public Task<bool> NotifyAsync(string notificationKey, Guid userId, string recipientEmail, Guid? claimId, NotificationType type, string? claimNumber = null, Guid? payoutId = null, CancellationToken cancellationToken = default)
        {
            SentNotifications.Add((notificationKey, userId, recipientEmail, type));
            return Task.FromResult(true);
        }

        public Task<bool> NotifyAsync(Guid userId, string recipientEmail, Guid? claimId, NotificationType type, string? claimNumber = null, CancellationToken cancellationToken = default)
        {
            SentNotifications.Add(($"claim:{claimId}:{type}", userId, recipientEmail, type));
            return Task.FromResult(true);
        }
    }

    private class StubUserEmailResolver : IUserEmailResolver
    {
        public Dictionary<Guid, string> Emails { get; } = new();
        public Task<string?> GetEmailAsync(Guid userId)
            => Task.FromResult(Emails.TryGetValue(userId, out var email) ? email : null);
    }
}
