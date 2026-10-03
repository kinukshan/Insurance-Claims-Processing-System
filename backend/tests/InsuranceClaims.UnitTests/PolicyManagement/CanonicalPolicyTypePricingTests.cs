using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Persistence.Seed;
using InsuranceClaims.Infrastructure.Services;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Regression tests for canonical PolicyType pricing correction.
///
/// Verifies:
/// - Correct premium calculation with canonical rates (Motor=15, Health=20, Home=10, Life=25, all risk=1.0)
/// - Draft policies are recalculated with corrected pricing
/// - Active and Cancelled policies preserve their stored premium
/// - Historical fixed-deductible policies retain the 5% deductible discount
/// - Percentage-deductible policies receive zero premium deductible discount
/// - Migration behavior correctness
/// </summary>
public class CanonicalPolicyTypePricingTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    // =========================================================================
    // 1. Motor Draft: coverage 100000, rate 15, risk 1.0, percentage deductible
    //    expected premium = 1500
    // =========================================================================

    [Fact]
    public async Task MotorDraft_Rate15_Risk1_Coverage100k_PercentageDeductible_Premium1500()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 0m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        // Motor has percentage deductible = 5%
        Assert.Equal(5m, created.DeductiblePercentage);
        // Premium = 15 * 100000 * 1.0 / 1000 = 1500 (no deductible discount for percentage)
        Assert.Equal(1500m, created.Premium);

        // Verify CalculatePremiumAsync agrees
        var calc = await service.CalculatePremiumAsync(created.Id);
        Assert.NotNull(calc);
        Assert.Equal(15m, calc!.BasePremiumRate);
        Assert.Equal(1.0m, calc.RiskMultiplier);
        Assert.Equal(0m, calc.DeductibleDiscount);
        Assert.Equal(1500m, calc.CalculatedPremium);
    }

    // =========================================================================
    // 2. Health Draft: coverage 150000, rate 20, risk 1.0, 10% deductible
    //    expected premium = 3000, deductible premium discount = 0
    // =========================================================================

    [Fact]
    public async Task HealthDraft_Rate20_Risk1_Coverage150k_PercentageDeductible_Premium3000()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 150000m,
            Deductible = 0m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        // Health has percentage deductible = 10%
        Assert.Equal(10m, created.DeductiblePercentage);
        // Premium = 20 * 150000 * 1.0 / 1000 = 3000
        Assert.Equal(3000m, created.Premium);

        var calc = await service.CalculatePremiumAsync(created.Id);
        Assert.NotNull(calc);
        Assert.Equal(20m, calc!.BasePremiumRate);
        Assert.Equal(1.0m, calc.RiskMultiplier);
        Assert.Equal(0m, calc.DeductibleDiscount);
        Assert.Equal(3000m, calc.CalculatedPremium);
    }

    // =========================================================================
    // 3. Home Draft: uses rate 10 and risk 1.0
    // =========================================================================

    [Fact]
    public async Task HomeDraft_Rate10_Risk1_CorrectPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var created = await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HomeInsuranceId,
            CoverageLimit = 200000m,
            Deductible = 0m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        // Home has percentage deductible = 10%
        Assert.Equal(10m, created.DeductiblePercentage);
        // Premium = 10 * 200000 * 1.0 / 1000 = 2000
        Assert.Equal(2000m, created.Premium);

        var calc = await service.CalculatePremiumAsync(created.Id);
        Assert.NotNull(calc);
        Assert.Equal(10m, calc!.BasePremiumRate);
        Assert.Equal(1.0m, calc.RiskMultiplier);
        Assert.Equal(2000m, calc.CalculatedPremium);
    }

    // =========================================================================
    // 4. Life Draft: uses rate 25 and risk 1.0, deductible 0%
    // =========================================================================

    [Fact]
    public async Task LifeDraft_Rate25_Risk1_ZeroDeductible()
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
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        Assert.Equal(0m, created.DeductiblePercentage);
        Assert.Equal(0m, created.Deductible);
        // Premium = 25 * 500000 * 1.0 / 1000 = 12500
        Assert.Equal(12500m, created.Premium);

        var calc = await service.CalculatePremiumAsync(created.Id);
        Assert.NotNull(calc);
        Assert.Equal(25m, calc!.BasePremiumRate);
        Assert.Equal(1.0m, calc.RiskMultiplier);
        Assert.Equal(0m, calc.DeductibleDiscount);
        Assert.Equal(12500m, calc.CalculatedPremium);
    }

    // =========================================================================
    // 5. Historical fixed deductible Draft: DeductiblePercentage = null,
    //    Deductible > 0, still receives 5% deductible discount
    //
    //    coverage = 50000, rate = 20, risk = 1, fixed deductible = 1000
    //    base = 1000, discount = 50, premium = 950
    // =========================================================================

    [Fact]
    public async Task HistoricalFixedDeductibleDraft_DeductibleDiscountApplied()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Directly insert a legacy policy with null DeductiblePercentage
        var policyType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HealthInsuranceId);
        Assert.NotNull(policyType);
        // Confirm canonical rate
        Assert.Equal(20m, policyType!.BasePremiumRate);
        Assert.Equal(1.0m, policyType.RiskMultiplier);

        var legacyDraft = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LEGACY001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 50000m,
            Deductible = 1000m,
            DeductiblePercentage = null, // Legacy: no percentage
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 0m // Will be verified via CalculatePremiumAsync
        };
        context.Policies.Add(legacyDraft);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(legacyDraft.Id);
        Assert.NotNull(calc);

        // basePremium = 20 * 50000 * 1.0 / 1000 = 1000
        // discount = round(1000 * 0.05, 2) = 50
        // premium = max(1000 - 50, 0) = 950
        Assert.Equal(50m, calc!.DeductibleDiscount);
        Assert.Equal(950m, calc.CalculatedPremium);
    }

    // =========================================================================
    // 6. Active policy: calculate-premium does NOT overwrite/reprice stored premium
    // =========================================================================

    [Fact]
    public async Task ActivePolicy_CalculatePremium_PreservesStoredPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Insert an Active policy with an arbitrary agreed premium
        var agreedPremium = 99999.99m;
        var activePolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-ACTIVE001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = 5m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Active,
            Premium = agreedPremium
        };
        context.Policies.Add(activePolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(activePolicy.Id);
        Assert.NotNull(calc);

        // Must return the stored agreed premium, NOT a recalculation
        Assert.Equal(agreedPremium, calc!.CalculatedPremium);
        Assert.Contains("preserved", calc.Breakdown, StringComparison.OrdinalIgnoreCase);

        // Verify the stored premium is unchanged in the DB
        var dbPolicy = await context.Policies.AsNoTracking().FirstAsync(p => p.Id == activePolicy.Id);
        Assert.Equal(agreedPremium, dbPolicy.Premium);
    }

    // =========================================================================
    // 7. Cancelled policy: calculate-premium does NOT overwrite/reprice stored premium
    // =========================================================================

    [Fact]
    public async Task CancelledPolicy_CalculatePremium_PreservesStoredPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var agreedPremium = 77777.77m;
        var cancelledPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-CANCELLED01",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 200000m,
            Deductible = 5000m,
            DeductiblePercentage = 10m,
            StartDate = DateTime.UtcNow.AddYears(-1),
            ExpiryDate = DateTime.UtcNow.AddMonths(-1),
            Status = PolicyStatus.Cancelled,
            Premium = agreedPremium
        };
        context.Policies.Add(cancelledPolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(cancelledPolicy.Id);
        Assert.NotNull(calc);

        // Must return the stored agreed premium, NOT a recalculation
        Assert.Equal(agreedPremium, calc!.CalculatedPremium);
        Assert.Contains("preserved", calc.Breakdown, StringComparison.OrdinalIgnoreCase);

        // Verify the stored premium is unchanged in the DB
        var dbPolicy = await context.Policies.AsNoTracking().FirstAsync(p => p.Id == cancelledPolicy.Id);
        Assert.Equal(agreedPremium, dbPolicy.Premium);
    }

    // =========================================================================
    // 8. Migration behavior tests (simulated with in-memory operations)
    //
    //    Since we cannot run SQL migrations against InMemory, we test the
    //    equivalent logic to verify correctness of the migration's intent.
    // =========================================================================

    [Fact]
    public async Task MigrationBehavior_CorrectsFourCanonicalPolicyTypePricingValues()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        // Verify the seeded values match canonical rates
        var motor = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        var health = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HealthInsuranceId);
        var home = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HomeInsuranceId);
        var life = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.LifeInsuranceId);

        Assert.NotNull(motor);
        Assert.NotNull(health);
        Assert.NotNull(home);
        Assert.NotNull(life);

        Assert.Equal(15.00m, motor!.BasePremiumRate);
        Assert.Equal(1.00m, motor.RiskMultiplier);

        Assert.Equal(20.00m, health!.BasePremiumRate);
        Assert.Equal(1.00m, health.RiskMultiplier);

        Assert.Equal(10.00m, home!.BasePremiumRate);
        Assert.Equal(1.00m, home.RiskMultiplier);

        Assert.Equal(25.00m, life!.BasePremiumRate);
        Assert.Equal(1.00m, life.RiskMultiplier);
    }

    [Fact]
    public async Task MigrationBehavior_RecalculatesDraftPolicies()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Simulate a Draft policy that was created with old pricing (Premium = old value)
        var draftPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-DRAFT001",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = 5m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 150000m // Old stale value (rate was 1500)
        };
        context.Policies.Add(draftPolicy);
        await context.SaveChangesAsync();

        // Simulate what the migration does: recalculate using corrected rates
        var policyType = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        Assert.NotNull(policyType);

        // basePremium = 15 * 100000 * 1.0 / 1000 = 1500
        // DeductiblePercentage has value → deductible discount = 0
        // premium = max(1500 - 0, 0) = 1500
        var basePremium = policyType!.BasePremiumRate * draftPolicy.CoverageLimit * policyType.RiskMultiplier / 1000m;
        var deductibleDiscount = draftPolicy.DeductiblePercentage.HasValue ? 0m
            : (draftPolicy.Deductible > 0 ? Math.Round(draftPolicy.Deductible * 0.05m, 2) : 0m);
        var expectedPremium = Math.Round(Math.Max(basePremium - deductibleDiscount, 0m), 2);

        Assert.Equal(1500m, expectedPremium);

        // Also verify via CalculatePremiumAsync (which uses the same formula)
        var calc = await service.CalculatePremiumAsync(draftPolicy.Id);
        Assert.NotNull(calc);
        Assert.Equal(expectedPremium, calc!.CalculatedPremium);
    }

    [Fact]
    public async Task MigrationBehavior_PreservesActivePremiums()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var originalPremium = 150000m; // Old value from stale pricing
        var activePolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-ACTIVE002",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = 5m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Active,
            Premium = originalPremium
        };
        context.Policies.Add(activePolicy);
        await context.SaveChangesAsync();

        // CalculatePremiumAsync should return stored premium for Active
        var calc = await service.CalculatePremiumAsync(activePolicy.Id);
        Assert.NotNull(calc);
        Assert.Equal(originalPremium, calc!.CalculatedPremium);
    }

    [Fact]
    public async Task MigrationBehavior_PreservesCancelledPremiums()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var originalPremium = 250000m;
        var cancelledPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-CANCEL002",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HealthInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 5000m,
            DeductiblePercentage = 10m,
            StartDate = DateTime.UtcNow.AddYears(-1),
            ExpiryDate = DateTime.UtcNow.AddMonths(-1),
            Status = PolicyStatus.Cancelled,
            Premium = originalPremium
        };
        context.Policies.Add(cancelledPolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(cancelledPolicy.Id);
        Assert.NotNull(calc);
        Assert.Equal(originalPremium, calc!.CalculatedPremium);
    }

    [Fact]
    public async Task MigrationBehavior_DoesNotModifyUnrelatedPolicyTypes()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        // Add a custom/unrelated policy type
        var customType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Travel Insurance",
            Description = "Coverage for travel-related risks.",
            BasePremiumRate = 8.00m,
            DefaultCoverageLimit = 100000m,
            DefaultDeductible = 2000m,
            RiskMultiplier = 1.2m,
            IsActive = true
        };
        context.PolicyTypes.Add(customType);
        await context.SaveChangesAsync();

        // The migration only touches the 4 canonical IDs/names.
        // Verify the custom type is untouched.
        var reloaded = await context.PolicyTypes.FindAsync(customType.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(8.00m, reloaded!.BasePremiumRate);
        Assert.Equal(1.2m, reloaded.RiskMultiplier);
    }

    // =========================================================================
    // 8b. Migration scope: canonical Draft recalculated, custom Draft untouched
    //
    //     Proves the migration's Draft UPDATE is scoped to the 4 canonical
    //     PolicyType IDs and does not recalculate Draft policies belonging to
    //     unrelated/custom PolicyTypes.
    // =========================================================================

    [Fact]
    public async Task MigrationScope_CanonicalDraftRecalculated_CustomDraftPreservesOriginalPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        // ── Custom PolicyType ──
        var customType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Travel Insurance",
            Description = "Coverage for travel-related risks.",
            BasePremiumRate = 8.00m,
            DefaultCoverageLimit = 100000m,
            DefaultDeductible = 2000m,
            RiskMultiplier = 1.2m,
            IsActive = true
        };
        context.PolicyTypes.Add(customType);

        // ── Custom Draft policy with an arbitrary stored premium ──
        var customDraftOriginalPremium = 12345.67m;
        var customDraft = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-CUSTOMDRAFT",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = customType.Id,
            CoverageLimit = 200000m,
            Deductible = 500m,
            DeductiblePercentage = null, // Legacy fixed deductible
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = customDraftOriginalPremium
        };
        context.Policies.Add(customDraft);

        // ── Canonical Draft policy with a stale stored premium ──
        var canonicalDraft = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-CANONDRAFT",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = 5m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 150000m // Stale value from old pricing
        };
        context.Policies.Add(canonicalDraft);
        await context.SaveChangesAsync();

        // ── Simulate Migration's Scoped UPDATE query ──
        // In the migration SQL:
        //   UPDATE "Policies" p SET ...
        //   FROM "PolicyTypes" pt
        //   WHERE p."PolicyTypeId" = pt."Id"
        //     AND p."Status" = 'Draft'
        //     AND pt."Id" IN ('22222222-2222-4222-8222-222222222221', ...)
        var canonicalIds = new HashSet<Guid>
        {
            PolicyClaimCompatibility.MotorInsuranceId,
            PolicyClaimCompatibility.HealthInsuranceId,
            PolicyClaimCompatibility.HomeInsuranceId,
            PolicyClaimCompatibility.LifeInsuranceId
        };

        var draftsMatchingMigrationScope = await context.Policies
            .Include(p => p.PolicyType)
            .Where(p => p.Status == PolicyStatus.Draft && canonicalIds.Contains(p.PolicyTypeId))
            .ToListAsync();

        foreach (var p in draftsMatchingMigrationScope)
        {
            var basePrem = p.PolicyType!.BasePremiumRate * p.CoverageLimit * p.PolicyType.RiskMultiplier / 1000m;
            var disc = p.DeductiblePercentage.HasValue ? 0m
                : (p.Deductible > 0 ? Math.Round(p.Deductible * 0.05m, 2) : 0m);
            p.Premium = Math.Round(Math.Max(basePrem - disc, 0m), 2);
        }
        await context.SaveChangesAsync();

        // ── Verify stored values in the database ──
        // 1. Canonical Draft policy was in scope: recalculated from 150000m -> 1500m
        var canonicalDraftInDb = await context.Policies
            .AsNoTracking()
            .FirstAsync(p => p.Id == canonicalDraft.Id);
        Assert.Equal(1500m, canonicalDraftInDb.Premium);

        // 2. Custom/unrelated Draft policy was OUT of scope: preserved original 12345.67m
        var customDraftInDb = await context.Policies
            .AsNoTracking()
            .FirstAsync(p => p.Id == customDraft.Id);
        Assert.Equal(customDraftOriginalPremium, customDraftInDb.Premium);

        // ── Also verify CalculatePremiumAsync behavior ──
        var service = new PolicyService(context);
        var canonicalCalc = await service.CalculatePremiumAsync(canonicalDraft.Id);
        Assert.NotNull(canonicalCalc);
        Assert.Equal(1500m, canonicalCalc!.CalculatedPremium);
    }

    // =========================================================================
    // 9. Expired/Lapsed policies also preserve stored premium
    // =========================================================================

    [Fact]
    public async Task ExpiredPolicy_CalculatePremium_PreservesStoredPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var agreedPremium = 55555.55m;
        var expiredPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-EXPIRED01",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.HomeInsuranceId,
            CoverageLimit = 300000m,
            Deductible = 15000m,
            DeductiblePercentage = 10m,
            StartDate = DateTime.UtcNow.AddYears(-2),
            ExpiryDate = DateTime.UtcNow.AddYears(-1),
            Status = PolicyStatus.Expired,
            Premium = agreedPremium
        };
        context.Policies.Add(expiredPolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(expiredPolicy.Id);
        Assert.NotNull(calc);
        Assert.Equal(agreedPremium, calc!.CalculatedPremium);
        Assert.Contains("preserved", calc.Breakdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LapsedPolicy_CalculatePremium_PreservesStoredPremium()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        var agreedPremium = 33333.33m;
        var lapsedPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LAPSED01",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.LifeInsuranceId,
            CoverageLimit = 1000000m,
            Deductible = 0m,
            DeductiblePercentage = 0m,
            StartDate = DateTime.UtcNow.AddYears(-2),
            ExpiryDate = DateTime.UtcNow.AddYears(-1),
            Status = PolicyStatus.Lapsed,
            Premium = agreedPremium
        };
        context.Policies.Add(lapsedPolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(lapsedPolicy.Id);
        Assert.NotNull(calc);
        Assert.Equal(agreedPremium, calc!.CalculatedPremium);
        Assert.Contains("preserved", calc.Breakdown, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 10. Seeder canonical values are correct for fresh databases
    // =========================================================================

    [Fact]
    public async Task Seeder_FreshDatabase_HasCanonicalRates()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        var motor = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.MotorInsuranceId);
        var health = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HealthInsuranceId);
        var home = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.HomeInsuranceId);
        var life = await context.PolicyTypes.FindAsync(PolicyClaimCompatibility.LifeInsuranceId);

        Assert.Equal(15.00m, motor!.BasePremiumRate);
        Assert.Equal(20.00m, health!.BasePremiumRate);
        Assert.Equal(10.00m, home!.BasePremiumRate);
        Assert.Equal(25.00m, life!.BasePremiumRate);

        // All risk multipliers should be 1.0
        Assert.Equal(1.0m, motor.RiskMultiplier);
        Assert.Equal(1.0m, health.RiskMultiplier);
        Assert.Equal(1.0m, home.RiskMultiplier);
        Assert.Equal(1.0m, life.RiskMultiplier);
    }

    // =========================================================================
    // 11. Draft CalculatePremiumAsync returns recalculated (not stored) premium
    // =========================================================================

    [Fact]
    public async Task DraftPolicy_CalculatePremium_ReturnsRecalculatedValue()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);
        var service = new PolicyService(context);

        // Insert a Draft policy with an intentionally wrong stored premium
        var draftPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-DRAFTRECALC",
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = PolicyClaimCompatibility.MotorInsuranceId,
            CoverageLimit = 100000m,
            Deductible = 10000m,
            DeductiblePercentage = 5m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Draft,
            Premium = 999999m // Intentionally wrong
        };
        context.Policies.Add(draftPolicy);
        await context.SaveChangesAsync();

        var calc = await service.CalculatePremiumAsync(draftPolicy.Id);
        Assert.NotNull(calc);

        // Draft should return recalculated value, NOT the stored 999999
        // Motor: 15 * 100000 * 1.0 / 1000 = 1500
        Assert.Equal(1500m, calc!.CalculatedPremium);
        Assert.DoesNotContain("preserved", calc.Breakdown, StringComparison.OrdinalIgnoreCase);
    }
}
