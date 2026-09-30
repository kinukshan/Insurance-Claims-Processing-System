using InsuranceClaims.Application.Common.Exceptions;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Unit tests for PayoutService covering:
/// - Claim status eligibility
/// - Duplicate payout protection (409)
/// - Policy/claim compatibility pre-check (400)
/// - Life Insurance payout project rule (claimed 80k/coverage 100k -> 80k, claimed 150k/coverage 100k -> 100k, defense-in-depth zero deductible)
/// - Atomic draft replacement & deterministic validation gating persistence.
/// </summary>
public class PayoutCalculationAndEligibilityTests
{
    // ── Supporting in-memory repository ──

    private class InMemoryPayoutRepository : IPayoutRepository
    {
        public readonly List<Payout> Payouts = new();
        public int DeleteCallCount { get; private set; }
        public int AddCallCount { get; private set; }

        public Task<Payout?> GetByIdAsync(Guid id) =>
            Task.FromResult(Payouts.FirstOrDefault(p => p.Id == id));

        public Task<Payout?> GetByClaimIdAsync(Guid claimId) =>
            Task.FromResult(Payouts.FirstOrDefault(p => p.ClaimId == claimId));

        public Task<List<Payout>> GetAllAsync() =>
            Task.FromResult(Payouts.ToList());

        public Task<(List<Payout> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, PayoutStatus? statusFilter, string? sortBy, bool sortDescending) =>
            Task.FromResult((Payouts.ToList(), Payouts.Count));

        public Task<(List<Payout> Items, int TotalCount)> GetPagedByPolicyholderAsync(
            Guid policyholderId, int page, int pageSize, PayoutStatus? statusFilter, string? sortBy, bool sortDescending) =>
            Task.FromResult((Payouts.ToList(), Payouts.Count));

        public Task<Payout> AddAsync(Payout payout)
        {
            AddCallCount++;
            Payouts.Add(payout);
            return Task.FromResult(payout);
        }

        public Task UpdateAsync(Payout payout) => Task.CompletedTask;

        public Task DeleteAsync(Payout payout)
        {
            DeleteCallCount++;
            Payouts.Remove(payout);
            return Task.CompletedTask;
        }
    }

    private class ConfigurablePayoutContextProvider : IPayoutContextProvider
    {
        public PayoutContext? Context { get; set; }

        public Task<PayoutContext?> GetPayoutContextAsync(Guid claimId) =>
            Task.FromResult(Context);
    }

    private class ConfigurableValidationAgent : IPayoutValidationAgentGateway
    {
        public bool ShouldPassValidation { get; set; } = true;
        public string ValidationSummary { get; set; } = "Deterministic validation passed.";

        public Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request)
        {
            return Task.FromResult(new PayoutValidationResult
            {
                Valid = ShouldPassValidation,
                Violations = ShouldPassValidation ? new List<string>() : new List<string> { "Deterministic violation: invalid proposal" },
                RequiresHumanApproval = true,
                Summary = ValidationSummary,
                AiUsed = false,
                FallbackUsed = false
            });
        }
    }

    // ── 1. Duplicate Payout Protection ──

    [Theory]
    [InlineData(PayoutStatus.PendingApproval)]
    [InlineData(PayoutStatus.Approved)]
    [InlineData(PayoutStatus.Processing)]
    [InlineData(PayoutStatus.Paid)]
    public async Task CalculatePayout_ExistingNonDraftPayout_ThrowsConflictException_AndRepoCountUnchanged(PayoutStatus existingStatus)
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        var existingPayout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Status = existingStatus,
            ProposedPayout = 50000m,
            FinalPayout = 50000m
        };
        await repo.AddAsync(existingPayout);
        var initialCount = repo.Payouts.Count;

        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 50000m,
            CoverageLimit = 100000m,
            Deductible = 500m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculatePayoutAsync(claimId));

        Assert.Contains(existingStatus.ToString(), ex.Message);
        // Authoritative: repository count must not increase on duplicate rejection
        Assert.Equal(initialCount, repo.Payouts.Count);
    }

    // ── 2. Compatibility Pre-Check ──

    [Fact]
    public async Task CalculatePayout_IncompatiblePolicyAndClaim_ThrowsPolicyClaimCompatibilityException_400()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Life Insurance",
            ClaimType = "Property", // Incompatible!
            ApprovedClaimAmount = 50000m,
            CoverageLimit = 100000m,
            Deductible = 0m
        };

        var ex = await Assert.ThrowsAsync<PolicyClaimCompatibilityException>(() =>
            service.CalculatePayoutAsync(claimId));

        Assert.Contains("Life Insurance", ex.Message);
        Assert.Contains("Property", ex.Message);
        Assert.Empty(repo.Payouts);
    }

    // ── 3. Life Payout Calculation Rule ──

    [Fact]
    public async Task CalculatePayout_LifeInsurance_ClaimUnderCoverage_PayoutEqualsClaimedAmount_WithZeroDeductible()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        // Claimed: 80,000 | Coverage: 100,000 | Deductible: 0
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Life Insurance",
            ClaimType = "Life",
            ApprovedClaimAmount = 80000m,
            CoverageLimit = 100000m,
            Deductible = 0m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.NotNull(result);
        Assert.Equal(0m, result.Deductible);
        Assert.Equal(80000m, result.ProposedPayout);
        Assert.Equal(80000m, result.FinalPayout);
        Assert.True(result.ValidationResult?.RequiresHumanApproval);
        Assert.Equal(PayoutStatus.PendingApproval, result.Status);
    }

    [Fact]
    public async Task CalculatePayout_LifeInsurance_ClaimExceedsCoverage_CapsAtCoverageLimit()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        // Claimed: 150,000 | Coverage: 100,000
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Life Insurance",
            ClaimType = "Life",
            ApprovedClaimAmount = 150000m,
            CoverageLimit = 100000m,
            Deductible = 0m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.NotNull(result);
        Assert.Equal(0m, result.Deductible);
        Assert.Equal(100000m, result.ProposedPayout);
        Assert.Equal(100000m, result.FinalPayout);
    }

    [Fact]
    public async Task CalculatePayout_TamperedLegacyLifePolicy_StillForcesEffectiveDeductibleToZero()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        // Crafted/tampered policy data has Deductible = 5000
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Life Insurance",
            ClaimType = "Life",
            ApprovedClaimAmount = 80000m,
            CoverageLimit = 100000m,
            Deductible = 5000m // Tampered!
        };

        var result = await service.CalculatePayoutAsync(claimId);

        // Effective deductible MUST be 0!
        Assert.Equal(0m, result.Deductible);
        Assert.Equal(80000m, result.FinalPayout);
    }

    [Fact]
    public async Task CalculatePayout_NonLifePolicy_AppliesStandardDeductible()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 10000m,
            CoverageLimit = 50000m,
            Deductible = 500m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(500m, result.Deductible);
        // 10,000 - 500 = 9,500
        Assert.Equal(9500m, result.FinalPayout);
    }

    // ── 4. Atomic Draft Replacement & Validation Gating ──

    [Fact]
    public async Task CalculatePayout_WhenCandidateValidationFails_ExistingDraftIsPreserved_AndNothingPersisted()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        // Existing Draft in repository
        var existingDraft = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Status = PayoutStatus.Draft,
            ProposedPayout = 20000m,
            FinalPayout = 20000m
        };
        await repo.AddAsync(existingDraft);

        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 30000m,
            CoverageLimit = 50000m,
            Deductible = 500m
        };

        // Configure deterministic validation to FAIL
        validator.ShouldPassValidation = false;

        // Must throw and NOT mutate repository
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculatePayoutAsync(claimId));

        // Existing draft must NOT have been deleted
        Assert.Equal(0, repo.DeleteCallCount);
        Assert.Single(repo.Payouts);
        Assert.Equal(existingDraft.Id, repo.Payouts[0].Id);
        Assert.Equal(PayoutStatus.Draft, repo.Payouts[0].Status);
    }

    [Fact]
    public async Task CalculatePayout_WhenCandidateValidationSucceeds_ReplacesOldDraft_AndPersistsAsPendingApproval()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new ConfigurableValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        var oldDraft = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            Status = PayoutStatus.Draft,
            ProposedPayout = 20000m,
            FinalPayout = 20000m
        };
        await repo.AddAsync(oldDraft);

        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 30000m,
            CoverageLimit = 50000m,
            Deductible = 500m
        };

        validator.ShouldPassValidation = true;

        var result = await service.CalculatePayoutAsync(claimId);

        // Old draft deleted and new one added
        Assert.Equal(1, repo.DeleteCallCount);
        Assert.Single(repo.Payouts);
        Assert.Equal(result.Id, repo.Payouts[0].Id);
        Assert.Equal(PayoutStatus.PendingApproval, repo.Payouts[0].Status);
    }
}
