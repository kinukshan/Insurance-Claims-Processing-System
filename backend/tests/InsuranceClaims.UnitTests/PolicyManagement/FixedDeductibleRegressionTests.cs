using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.Common.Exceptions;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Services;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Persistence.Seed;
using InsuranceClaims.Infrastructure.Services;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Regression tests specifically verifying fixed deductibles for all insurance types,
/// anti-tampering during creation and edit, and payout calculations for claims below,
/// equal to, and above the deductible.
/// </summary>
public class FixedDeductibleRegressionTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private class InMemoryPayoutRepository : IPayoutRepository
    {
        public readonly List<Payout> Payouts = new();

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
            Payouts.Add(payout);
            return Task.FromResult(payout);
        }

        public Task UpdateAsync(Payout payout) => Task.CompletedTask;

        public Task DeleteAsync(Payout payout)
        {
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

    private class PassingValidationAgent : IPayoutValidationAgentGateway
    {
        public Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request)
        {
            return Task.FromResult(new PayoutValidationResult
            {
                Valid = true,
                Violations = new List<string>(),
                RequiresHumanApproval = true,
                Summary = "Validation passed.",
                AiUsed = false,
                FallbackUsed = false
            });
        }
    }

    // =========================================================================
    // SECTION A: Policy Creation Fixed Deductibles & Anti-Tampering
    // =========================================================================

    [Fact]
    public async Task CreatePolicy_MotorInsurance_Receives10000Deductible()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 0m, // Client attempt to set 0
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(10000m, result.Deductible);
        var persisted = await context.Policies.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(10000m, persisted.Deductible);
    }

    [Fact]
    public async Task CreatePolicy_HealthInsurance_Receives5000Deductible()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 50000m,
            Deductible = 250m, // Client attempt to set arbitrary amount
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(5000m, result.Deductible);
        var persisted = await context.Policies.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(5000m, persisted.Deductible);
    }

    [Fact]
    public async Task CreatePolicy_HomeInsurance_Receives15000Deductible()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HomeInsuranceId,
            CoverageLimit = 300000m,
            Deductible = 1000m, // Client attempt
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(15000m, result.Deductible);
        var persisted = await context.Policies.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(15000m, persisted.Deductible);
    }

    [Fact]
    public async Task CreatePolicy_LifeInsurance_Receives0Deductible()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.LifeInsuranceId,
            CoverageLimit = 250000m,
            Deductible = 5000m, // Client attempt to set 5000
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(0m, result.Deductible);
        var persisted = await context.Policies.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(0m, persisted.Deductible);
    }

    [Theory]
    [InlineData("22222222-2222-4222-8222-222222222221", 0, 10000)]
    [InlineData("22222222-2222-4222-8222-222222222221", 999999, 10000)]
    [InlineData("22222222-2222-4222-8222-222222222222", 0, 5000)]
    [InlineData("22222222-2222-4222-8222-222222222222", 25000, 5000)]
    [InlineData("22222222-2222-4222-8222-222222222223", 0, 15000)]
    [InlineData("22222222-2222-4222-8222-222222222223", 500, 15000)]
    [InlineData("22222222-2222-4222-8222-222222222224", 5000, 0)]
    [InlineData("22222222-2222-4222-8222-222222222224", 20000, 0)]
    public async Task MaliciousClientInput_CannotOverrideFixedDeductible(string typeIdStr, decimal clientDeductible, decimal expectedDeductible)
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.Parse(typeIdStr),
            CoverageLimit = 100000m,
            Deductible = clientDeductible,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(expectedDeductible, result.Deductible);
    }

    [Fact]
    public async Task CreatePolicy_UnknownInsuranceType_HandledSafelyWithArgumentException()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var unknownTypeId = Guid.NewGuid();
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = unknownTypeId,
            CoverageLimit = 100000m,
            Deductible = 500m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
        Assert.Contains(unknownTypeId.ToString(), ex.Message);
    }

    [Fact]
    public async Task ExistingPolicy_NonfinancialEdit_PreservesOriginalAgreedDeductible()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // An existing legacy motor policy with grandfathered $2,500 deductible
        var legacyPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LEGACY-001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 2500m, // Grandfathered agreed amount
            Premium = 1500m,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Status = PolicyStatus.Active
        };
        context.Policies.Add(legacyPolicy);
        await context.SaveChangesAsync();

        // Policy edit request (e.g. updating coverage limit or exclusions)
        var updateDto = new UpdatePolicyDto
        {
            CoverageLimit = 120000m,
            Deductible = 50000m, // Attempting to alter deductible via edit
            Exclusions = "Updated exclusion list"
        };

        var result = await service.UpdateAsync(legacyPolicy.Id, updateDto);

        // Agreemeent preserved: still 2500m!
        Assert.NotNull(result);
        Assert.Equal(2500m, result.Deductible);
        var refreshed = await context.Policies.FindAsync(legacyPolicy.Id);
        Assert.NotNull(refreshed);
        Assert.Equal(2500m, refreshed.Deductible);
        Assert.Equal(120000m, refreshed.CoverageLimit);
    }

    // =========================================================================
    // SECTION B: Payout Calculations & Deductible Rules
    // =========================================================================

    [Fact]
    public async Task CalculatePayout_EligibleMotorClaim5000_With10000Deductible_ProducesZeroPayout_WithExplanation()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 5000m,
            CoverageLimit = 50000m,
            Deductible = 10000m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(0m, result.FinalPayout);
        Assert.Equal(10000m, result.Deductible);
        Assert.Equal(5000m, result.ApprovedClaimAmount);
        Assert.NotNull(result.Explanation);
        Assert.Contains("does not exceed your policy deductible", result.Explanation);
    }

    [Fact]
    public async Task CalculatePayout_EligibleMotorClaim10000_With10000Deductible_ProducesZeroPayout_WithExplanation()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
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
            Deductible = 10000m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(0m, result.FinalPayout);
        Assert.Equal(10000m, result.Deductible);
        Assert.NotNull(result.Explanation);
        Assert.Contains("does not exceed your policy deductible", result.Explanation);
    }

    [Fact]
    public async Task CalculatePayout_EligibleMotorClaim30000_With10000Deductible_Produces20000Payout()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 30000m,
            CoverageLimit = 50000m,
            Deductible = 10000m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(20000m, result.FinalPayout);
        Assert.Equal(10000m, result.Deductible);
        Assert.Null(result.Explanation);
    }

    [Fact]
    public async Task CalculatePayout_EnforcesCoverageLimit_BeforeDeductibleSubtraction()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        // Approved Claim: 60,000 | Coverage Limit: 50,000 | Deductible: 10,000
        // Eligible Amount = min(60000, 50000) = 50000
        // Final Payout = max(0, 50000 - 10000) = 40000
        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 60000m,
            CoverageLimit = 50000m,
            Deductible = 10000m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(40000m, result.FinalPayout);
    }

    [Fact]
    public async Task CalculatePayout_NeverProducesNegativePayout()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        // Approved Claim: 2,000 | Deductible: 15,000 (Home Insurance)
        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Home Insurance",
            ClaimType = "Property",
            ApprovedClaimAmount = 2000m,
            CoverageLimit = 200000m,
            Deductible = 15000m
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(0m, result.FinalPayout);
        Assert.True(result.FinalPayout >= 0m);
    }

    [Fact]
    public async Task CalculatePayout_LifeInsurance_AppliesZeroDeductible()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var claimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = claimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Life Insurance",
            ClaimType = "Life",
            ApprovedClaimAmount = 75000m,
            CoverageLimit = 100000m,
            Deductible = 9999m // Even if tampered in context, must be 0
        };

        var result = await service.CalculatePayoutAsync(claimId);

        Assert.Equal(0m, result.Deductible);
        Assert.Equal(75000m, result.FinalPayout);
    }

    [Fact]
    public async Task ExecutePayoutAsync_ZeroValuePayout_ThrowsInvalidOperationException_PreventingGatewayCall()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var zeroPayout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            ApprovedClaimAmount = 5000m,
            CoverageLimit = 50000m,
            Deductible = 10000m,
            ProposedPayout = 0m,
            FinalPayout = 0m,
            Status = PayoutStatus.Approved
        };
        await repo.AddAsync(zeroPayout);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecutePayoutAsync(zeroPayout.Id));

        Assert.Contains("Cannot execute a zero-value payout", ex.Message);
        Assert.Equal(PayoutStatus.Approved, zeroPayout.Status); // Status unchanged, never moved to Paid
    }

    [Fact]
    public async Task HistoricalPayouts_AreNotModified_WhenNewCalculationsOccur()
    {
        var repo = new InMemoryPayoutRepository();
        var contextProvider = new ConfigurablePayoutContextProvider();
        var validator = new PassingValidationAgent();
        var service = new PayoutService(repo, contextProvider, validator);

        var historicalPayout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            ApprovedClaimAmount = 15000m,
            CoverageLimit = 50000m,
            Deductible = 500m, // Historical deductible
            ProposedPayout = 14500m,
            FinalPayout = 14500m,
            Status = PayoutStatus.Paid
        };
        await repo.AddAsync(historicalPayout);

        // New calculation on a completely different claim
        var newClaimId = Guid.NewGuid();
        contextProvider.Context = new PayoutContext
        {
            ClaimId = newClaimId,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Motor Insurance",
            ClaimType = "Motor",
            ApprovedClaimAmount = 30000m,
            CoverageLimit = 50000m,
            Deductible = 10000m
        };
        await service.CalculatePayoutAsync(newClaimId);

        var verified = await repo.GetByIdAsync(historicalPayout.Id);
        Assert.NotNull(verified);
        Assert.Equal(14500m, verified.FinalPayout);
        Assert.Equal(500m, verified.Deductible);
        Assert.Equal(PayoutStatus.Paid, verified.Status);
    }
}
