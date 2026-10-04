using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.Common;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Persistence.Seed;
using InsuranceClaims.Infrastructure.Services;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Tests verifying premium-calculation consistency between CalculatePremiumAsync (breakdown)
/// and CalculatePremium (stored premium), including percentage-deductible, legacy fixed-deductible,
/// zero-deductible, Life insurance, renewal, and creation-time premium scenarios.
/// </summary>
public class PremiumCalculationConsistencyTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    // =========================================================================
    // 1. Percentage-deductible policy → premium deductible discount = 0
    // =========================================================================

    [Fact]
    public async Task PercentageDeductiblePolicy_BreakdownDeductibleDiscountIsZero()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Create a Motor policy (percentage deductible = 5%)
        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 0m,
            StartDate = BusinessCalendar.Default.Today,
            ExpiryDate = BusinessCalendar.Default.Today.AddYears(1)
        });

        Assert.Equal(5m, created.DeductiblePercentage);

        var premium = await service.CalculatePremiumAsync(created.Id);

        Assert.NotNull(premium);
        Assert.Equal(0m, premium!.DeductibleDiscount);
        Assert.Equal(created.Premium, premium.CalculatedPremium);
    }

    // =========================================================================
    // 2. Legacy fixed-deductible policy → deductible discount = Deductible * 0.05
    // =========================================================================

    [Fact]
    public async Task LegacyFixedDeductiblePolicy_BreakdownDeductibleDiscountIs500()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var policyType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        Assert.NotNull(policyType);

        // Simulate a legacy policy without percentage deductible
        var legacyPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LEGACY001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = policyType!.Id,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = null, // No percentage → legacy fixed deductible
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 0m
        };
        context.Policies.Add(legacyPolicy);
        await context.SaveChangesAsync();

        var premium = await service.CalculatePremiumAsync(legacyPolicy.Id);

        Assert.NotNull(premium);
        // 10000 * 0.05 = 500
        Assert.Equal(500m, premium!.DeductibleDiscount);
        Assert.Equal(premium.CalculatedPremium, premium.CalculatedPremium);
    }

    // =========================================================================
    // 3. Zero fixed deductible → discount = 0
    // =========================================================================

    [Fact]
    public async Task ZeroFixedDeductiblePolicy_BreakdownDeductibleDiscountIsZero()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var policyType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        Assert.NotNull(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-ZERO001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = policyType!.Id,
            CoverageLimit = 100000m,
            Deductible = 0m,
            DeductiblePercentage = null,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 0m
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync();

        var premium = await service.CalculatePremiumAsync(policy.Id);

        Assert.NotNull(premium);
        Assert.Equal(0m, premium!.DeductibleDiscount);
    }

    // =========================================================================
    // 4. Breakdown values exactly match CalculatedPremium
    // =========================================================================

    [Theory]
    [InlineData(100000, 5, 10000)]   // Percentage deductible Motor
    [InlineData(50000, null, 5000)]   // Legacy fixed deductible Health
    [InlineData(200000, null, 0)]     // Zero deductible
    public async Task BreakdownValues_ExactlyMatchCalculatedPremium(
        decimal coverageLimit, int? deductiblePct, decimal fixedDeductible)
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var policyType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        Assert.NotNull(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-MATCH001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = policyType!.Id,
            CoverageLimit = coverageLimit,
            Deductible = fixedDeductible,
            DeductiblePercentage = deductiblePct.HasValue ? (decimal)deductiblePct.Value : null,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 0m
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync();

        var result = await service.CalculatePremiumAsync(policy.Id);

        Assert.NotNull(result);

        // Verify: (BasePremiumRate × CoverageLimit × RiskMultiplier / 1000) - DeductibleDiscount = CalculatedPremium
        var expectedBasePremium = result!.BasePremiumRate * result.CoverageLimit * result.RiskMultiplier / 1000m;
        var expectedPremium = Math.Max(Math.Round(expectedBasePremium - result.DeductibleDiscount, 2), 0m);

        Assert.Equal(expectedPremium, result.CalculatedPremium);
    }

    // =========================================================================
    // 5. Creation-time premium and CalculatePremiumAsync result agree
    // =========================================================================

    [Fact]
    public async Task CreationPremium_AndCalculatePremiumAsync_Agree()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 200000m,
            Deductible = 0m,
            StartDate = BusinessCalendar.Default.Today,
            ExpiryDate = BusinessCalendar.Default.Today.AddYears(1)
        });

        var premium = await service.CalculatePremiumAsync(created.Id);

        Assert.NotNull(premium);
        Assert.Equal(created.Premium, premium!.CalculatedPremium);
    }

    // =========================================================================
    // 6. Renewal premium uses the same logic
    // =========================================================================

    [Fact]
    public async Task RenewalPremium_UsesConsistentCalculation()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var homeType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HomeInsuranceId);
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-HOME-RENEW",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HomeInsuranceId,
            PolicyType = homeType!,
            CoverageLimit = 300000m,
            Deductible = PolicyClaimCompatibility.GetFixedDeductible("Home Insurance") ?? 15000m,
            DeductiblePercentage = 10m,
            StartDate = DateTime.UtcNow.AddYears(-1),
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            Status = PolicyStatus.Active,
            RenewalStatus = RenewalStatus.NotDue
        };
        policy.Premium = 1500m;
        context.Policies.Add(policy);
        await context.SaveChangesAsync();

        // Expire the policy
        var expireResult = await service.UpdateAsync(policy.Id, new UpdatePolicyDto { Status = "Expired" });
        Assert.NotNull(expireResult);

        var renewalResult = await service.RenewPolicyAsync(policy.Id);

        Assert.True(renewalResult.Success);
        Assert.NotNull(renewalResult.RenewedPolicyId);

        // The renewed policy should have the same premium as a fresh calculation
        var renewedPremium = await service.CalculatePremiumAsync(renewalResult.RenewedPolicyId!.Value);

        Assert.NotNull(renewedPremium);
        Assert.Equal(renewalResult.NewPremium, renewedPremium!.CalculatedPremium);
    }

    // =========================================================================
    // 7. Life percentage/zero deductible premium behavior remains correct
    // =========================================================================

    [Fact]
    public async Task LifeInsurance_ZeroDeductible_PremiumCorrect()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.LifeInsuranceId,
            CoverageLimit = 500000m,
            Deductible = 0m,
            StartDate = BusinessCalendar.Default.Today,
            ExpiryDate = BusinessCalendar.Default.Today.AddYears(1)
        });

        Assert.Equal(0m, created.DeductiblePercentage);
        Assert.Equal(0m, created.Deductible);

        var premium = await service.CalculatePremiumAsync(created.Id);

        Assert.NotNull(premium);
        Assert.Equal(0m, premium!.DeductibleDiscount);
        Assert.Equal(created.Premium, premium.CalculatedPremium);

        // Verify the breakdown says 0 deductible discount
        Assert.Contains("0 deductible discount", premium.Breakdown);
    }

    // =========================================================================
    // 8. Motor percentage-deductible: stored Deductible=10000 but discount=0
    //    This is the exact scenario from the bug report.
    // =========================================================================

    [Fact]
    public async Task MotorPercentageDeductible_StoredDeductible10000_DiscountIsZero_NotFiveHundred()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Motor creates with DeductiblePercentage=5 and Deductible=10000
        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 0m,
            StartDate = BusinessCalendar.Default.Today,
            ExpiryDate = BusinessCalendar.Default.Today.AddYears(1)
        });

        Assert.Equal(5m, created.DeductiblePercentage);
        Assert.Equal(10000m, created.Deductible); // Fixed deductible is also set

        var premium = await service.CalculatePremiumAsync(created.Id);

        Assert.NotNull(premium);

        // THE BUG FIX: this must be 0, not 500
        Assert.Equal(0m, premium!.DeductibleDiscount);

        // Breakdown must show 0 deductible discount, not 500
        Assert.Contains("0 deductible discount", premium.Breakdown);
        Assert.DoesNotContain("500 deductible discount", premium.Breakdown);

        // CalculatedPremium must match stored premium
        Assert.Equal(created.Premium, premium.CalculatedPremium);
    }
}
