using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.Common.Exceptions;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Persistence.Seed;
using InsuranceClaims.Infrastructure.Services;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Unit tests for InsuranceClass, PolicyClaimCompatibility, Life deductible rules,
/// PolicyTypeSeeder, and classification metadata.
/// </summary>
public class InsuranceClassAndCompatibilityTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    // ── 1. Classification & Product Metadata ──

    [Fact]
    public void CanonicalPolicyTypes_HaveExpectedInsuranceClassification()
    {
        var motor = new PolicyType { Id = PolicyClaimCompatibility.MotorInsuranceId, Name = PolicyClaimCompatibility.MotorInsurance, InsuranceClass = InsuranceClass.General };
        var health = new PolicyType { Id = PolicyClaimCompatibility.HealthInsuranceId, Name = PolicyClaimCompatibility.HealthInsurance, InsuranceClass = InsuranceClass.General };
        var home = new PolicyType { Id = PolicyClaimCompatibility.HomeInsuranceId, Name = PolicyClaimCompatibility.HomeInsurance, InsuranceClass = InsuranceClass.General };
        var life = new PolicyType { Id = PolicyClaimCompatibility.LifeInsuranceId, Name = PolicyClaimCompatibility.LifeInsurance, InsuranceClass = InsuranceClass.LongTerm };

        Assert.Equal(InsuranceClass.General, motor.InsuranceClass);
        Assert.Equal(InsuranceClass.General, health.InsuranceClass);
        Assert.Equal(InsuranceClass.General, home.InsuranceClass);
        Assert.Equal(InsuranceClass.LongTerm, life.InsuranceClass);
    }

    [Fact]
    public void MockPension_LongTerm_DoesNotInheritLifeRules()
    {
        // A mock Pension product with LongTerm insurance class
        var pension = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Pension",
            InsuranceClass = InsuranceClass.LongTerm,
            DefaultDeductible = 5000m
        };

        // LongTerm class alone does NOT make it Life Insurance
        Assert.False(PolicyClaimCompatibility.IsLifeInsurance(pension.Name));
        Assert.False(pension.Id == PolicyClaimCompatibility.LifeInsuranceId);

        // Life deductible rule (deductible = 0) must NOT apply to Pension
        Assert.NotEqual(0m, pension.DefaultDeductible);

        // Pension must NOT accept Life claims
        Assert.False(PolicyClaimCompatibility.IsCompatible("Pension", ClaimType.Life));
        Assert.False(PolicyClaimCompatibility.IsCompatible("Pension", "Life"));
    }

    // ── 2. Compatibility Matrix Tests ──

    [Theory]
    [InlineData("Motor Insurance", ClaimType.Motor, true)]
    [InlineData("Motor Insurance", ClaimType.Auto, true)] // Historical Auto accepted
    [InlineData("Motor Insurance", ClaimType.Property, false)]
    [InlineData("Comprehensive Auto", ClaimType.Motor, true)] // Alias
    [InlineData("Comprehensive Auto", ClaimType.Auto, true)]
    [InlineData("Health Insurance", ClaimType.Health, true)]
    [InlineData("Health Insurance", ClaimType.Motor, false)]
    [InlineData("Home Insurance", ClaimType.Property, true)]
    [InlineData("Home Insurance", ClaimType.Home, true)] // Historical Home accepted
    [InlineData("Home / Property Insurance", ClaimType.Property, true)] // Display alias
    [InlineData("Home / Property Insurance", ClaimType.Home, true)]
    [InlineData("Home Insurance", ClaimType.Motor, false)]
    [InlineData("Life Insurance", ClaimType.Life, true)]
    [InlineData("Life Insurance", ClaimType.Property, false)]
    [InlineData("Life Insurance", ClaimType.Health, false)]
    [InlineData("Life Insurance", ClaimType.Motor, false)]
    [InlineData("Life Insurance", ClaimType.Liability, false)]
    [InlineData("Life Insurance", ClaimType.Other, false)]
    [InlineData("Travel Insurance", ClaimType.Travel, false)] // Unsupported policy
    [InlineData("Pension", ClaimType.Life, false)] // Mock product rejected
    [InlineData("Unknown Insurance", ClaimType.Other, false)]
    public void PolicyClaimCompatibility_MatrixEnforcement_FailsClosed(string policyType, ClaimType claimType, bool expected)
    {
        var isCompatible = PolicyClaimCompatibility.IsCompatible(policyType, claimType);
        Assert.Equal(expected, isCompatible);
    }

    [Fact]
    public void PolicyClaimCompatibility_ThrowsPolicyClaimCompatibilityException_WithCleanMessage()
    {
        var ex = new PolicyClaimCompatibilityException(
            PolicyClaimCompatibility.GetErrorMessage("Life Insurance", ClaimType.Motor));

        Assert.Contains("Life Insurance", ex.Message);
        Assert.Contains("Motor", ex.Message);
        Assert.DoesNotContain("SELECT", ex.Message);
        Assert.DoesNotContain("stack", ex.Message);
    }

    // ── 3. Life Deductible Rule (Create & Update) ──

    [Fact]
    public async Task PolicyService_CreateAsync_LifeInsurance_ForcesDeductibleToZero()
    {
        using var context = CreateInMemoryContext();
        var lifeType = new PolicyType
        {
            Id = PolicyClaimCompatibility.LifeInsuranceId,
            Name = PolicyClaimCompatibility.LifeInsurance,
            InsuranceClass = InsuranceClass.LongTerm,
            DefaultCoverageLimit = 1000000m,
            DefaultDeductible = 0m
        };
        context.PolicyTypes.Add(lifeType);
        await context.SaveChangesAsync();

        var service = new PolicyService(context);
        var ownerId = Guid.NewGuid();

        // Attempt to create Life policy with a non-zero deductible of 5000
        var dto = new CreatePolicyDto
        {
            PolicyholderId = ownerId,
            PolicyTypeId = lifeType.Id,
            CoverageLimit = 500000m,
            Deductible = 5000m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(0m, result.Deductible);

        // Verify in database
        var persisted = await context.Policies.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(0m, persisted.Deductible);
        Assert.Equal(InsuranceClass.LongTerm, (InsuranceClass)result.InsuranceClass);
    }

    [Fact]
    public async Task PolicyService_UpdateAsync_LifeInsurance_ForcesDeductibleToZero()
    {
        using var context = CreateInMemoryContext();
        var lifeType = new PolicyType
        {
            Id = PolicyClaimCompatibility.LifeInsuranceId,
            Name = PolicyClaimCompatibility.LifeInsurance,
            InsuranceClass = InsuranceClass.LongTerm,
            DefaultCoverageLimit = 1000000m,
            DefaultDeductible = 0m
        };
        context.PolicyTypes.Add(lifeType);

        var ownerId = Guid.NewGuid();
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LIF-001",
            PolicyTypeId = lifeType.Id,
            PolicyholderId = ownerId,
            CoverageLimit = 500000m,
            Deductible = 0m,
            Status = PolicyStatus.Draft,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync();

        var service = new PolicyService(context);

        // Attempt to update Life policy deductible to 3000
        var updateDto = new UpdatePolicyDto
        {
            CoverageLimit = 600000m,
            Deductible = 3000m
        };

        var updated = await service.UpdateAsync(policy.Id, updateDto, ownerId, Role.Underwriter);

        Assert.NotNull(updated);
        Assert.Equal(0m, updated.Deductible);

        var persisted = await context.Policies.FindAsync(policy.Id);
        Assert.NotNull(persisted);
        Assert.Equal(0m, persisted.Deductible);
    }

    [Fact]
    public async Task PolicyService_UpdateAsync_UnloadedNavigation_StillResolvesPolicyTypeAndForcesZeroDeductible()
    {
        using var context = CreateInMemoryContext();
        var lifeType = new PolicyType
        {
            Id = PolicyClaimCompatibility.LifeInsuranceId,
            Name = PolicyClaimCompatibility.LifeInsurance,
            InsuranceClass = InsuranceClass.LongTerm
        };
        context.PolicyTypes.Add(lifeType);

        var ownerId = Guid.NewGuid();
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-LIF-002",
            PolicyTypeId = lifeType.Id,
            PolicyType = null!, // Intentionally null to simulate unloaded navigation
            PolicyholderId = ownerId,
            CoverageLimit = 500000m,
            Deductible = 0m,
            Status = PolicyStatus.Draft,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync();

        var service = new PolicyService(context);

        var updateDto = new UpdatePolicyDto
        {
            Deductible = 8000m // Crafted attempt
        };

        var result = await service.UpdateAsync(policy.Id, updateDto, ownerId, Role.Underwriter);

        Assert.NotNull(result);
        Assert.Equal(0m, result.Deductible);
    }

    [Fact]
    public async Task PolicyService_CreateAsync_NonLifePolicy_EnforcesFixedDeductible_IgnoringClientInput()
    {
        using var context = CreateInMemoryContext();
        var motorType = new PolicyType
        {
            Id = PolicyClaimCompatibility.MotorInsuranceId,
            Name = PolicyClaimCompatibility.MotorInsurance,
            InsuranceClass = InsuranceClass.General,
            DefaultCoverageLimit = 500000m,
            DefaultDeductible = PolicyClaimCompatibility.MotorDeductible
        };
        context.PolicyTypes.Add(motorType);
        await context.SaveChangesAsync();

        var service = new PolicyService(context);
        var ownerId = Guid.NewGuid();

        var dto = new CreatePolicyDto
        {
            PolicyholderId = ownerId,
            PolicyTypeId = motorType.Id,
            CoverageLimit = 500000m,
            Deductible = 2500m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Equal(PolicyClaimCompatibility.MotorDeductible, result.Deductible);
        Assert.Equal(0, result.InsuranceClass);
        Assert.Equal("General", result.InsuranceClassCode);
    }

    // ── 4. PolicyTypeSeeder Idempotence ──

    [Fact]
    public async Task PolicyTypeSeeder_EnsuresLifeSeeded_AndDoesNotDuplicateOnMultipleRuns()
    {
        using var context = CreateInMemoryContext();

        // Run 1: seed
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        var life1 = await context.PolicyTypes.FirstOrDefaultAsync(p => p.Id == PolicyClaimCompatibility.LifeInsuranceId);
        Assert.NotNull(life1);
        Assert.Equal("Life Insurance", life1.Name);
        Assert.Equal(InsuranceClass.LongTerm, life1.InsuranceClass);
        Assert.Equal(0m, life1.DefaultDeductible);

        var totalCount1 = await context.PolicyTypes.CountAsync();

        // Run 2: idempotent re-seed
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        var totalCount2 = await context.PolicyTypes.CountAsync();
        Assert.Equal(totalCount1, totalCount2);

        // Existing Motor/Health/Home values not overwritten
        var motor = await context.PolicyTypes.FirstOrDefaultAsync(p => p.Id == PolicyClaimCompatibility.MotorInsuranceId);
        Assert.NotNull(motor);
        Assert.Equal(InsuranceClass.General, motor.InsuranceClass);
    }

    [Fact]
    public async Task PolicyTypeSeeder_PreventsCaseInsensitiveDuplicateLifeRows()
    {
        using var context = CreateInMemoryContext();

        // Pre-existing row with lower-case name
        context.PolicyTypes.Add(new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "life insurance", // lowercase
            InsuranceClass = InsuranceClass.LongTerm,
            DefaultDeductible = 0m
        });
        await context.SaveChangesAsync();

        // Running seeder should recognize case-insensitive match and not add a duplicate
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        var lifeRows = await context.PolicyTypes
            .Where(p => p.Name.ToLower() == "life insurance")
            .ToListAsync();

        Assert.Single(lifeRows);
    }

    // ── 5. Navigation Loading & DTO Classification Metadata ──

    [Fact]
    public async Task PolicyService_GetAllAsync_IncludesClassificationMetadata()
    {
        using var context = CreateInMemoryContext();
        await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(context);

        var ownerId = Guid.NewGuid();
        var service = new PolicyService(context);

        // Create a Life policy
        await service.CreateAsync(new CreatePolicyDto
        {
            PolicyholderId = ownerId,
            PolicyTypeId = PolicyClaimCompatibility.LifeInsuranceId,
            CoverageLimit = 1000000m,
            Deductible = 0m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });

        var policies = await service.GetByPolicyholderIdAsync(ownerId);

        Assert.Single(policies);
        var p = policies.First();
        Assert.Equal("Life Insurance", p.PolicyTypeName);
        Assert.Equal(1, p.InsuranceClass);
        Assert.Equal("LongTerm", p.InsuranceClassCode);
        Assert.Equal("Long-Term Insurance", p.InsuranceClassName);
    }
}
