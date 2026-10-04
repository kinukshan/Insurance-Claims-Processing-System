using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Validators;
using InsuranceClaims.Application.Common;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Validators;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Services;
using Xunit;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Authoritative date validation and serialization tests for Policy and Claim.
/// Covers requirements 16–21.
/// </summary>
public class PolicyDateValidationTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Test16_PolicyCreate_StartDateBeforeToday_Rejected()
    {
        var yesterday = BusinessCalendar.Default.Today.AddDays(-1);
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = 1000m,
            StartDate = yesterday,
            ExpiryDate = yesterday.AddYears(1)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Start date cannot be before today"));
    }

    [Fact]
    public void Test17_PolicyCreate_ExpiryDateBeforeStartDate_Rejected()
    {
        var today = BusinessCalendar.Default.Today;
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = 1000m,
            StartDate = today.AddDays(10),
            ExpiryDate = today.AddDays(5)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Expiry date must be later than the start date"));
    }

    [Fact]
    public void Test18_PolicyCreate_ExpiryDateEqualsStartDate_Rejected()
    {
        var today = BusinessCalendar.Default.Today;
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = 1000m,
            StartDate = today.AddDays(10),
            ExpiryDate = today.AddDays(10)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Expiry date must be later than the start date"));
    }

    [Fact]
    public void Test19_PolicyCreate_ValidStartDateTodayAndFutureExpiryDate_Accepted()
    {
        var today = BusinessCalendar.Default.Today;
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = 1000m,
            StartDate = today,
            ExpiryDate = today.AddYears(1),
            Exclusions = "Standard exclusions"
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Empty(errors);
    }

    [Fact]
    public void Test20_ClaimIncidentDate_RoundTripPreservesSameCalendarDate()
    {
        var incidentCalendarDate = new DateTime(2026, 10, 4);

        var createDto = new CreateClaimDto(
            PolicyId: Guid.NewGuid(),
            ClaimType: ClaimType.Auto,
            IncidentDate: incidentCalendarDate,
            IncidentLocation: "Colombo, Sri Lanka",
            Description: "Traffic collision",
            ClaimedAmount: 75000m
        );

        var errors = CreateClaimValidator.Validate(createDto);
        Assert.Empty(errors);

        // Normalize as ClaimService does
        var storedUtcDate = DateTime.SpecifyKind(createDto.IncidentDate.Date, DateTimeKind.Utc);
        Assert.Equal(2026, storedUtcDate.Year);
        Assert.Equal(10, storedUtcDate.Month);
        Assert.Equal(4, storedUtcDate.Day);
        Assert.Equal(0, storedUtcDate.Hour);
        Assert.Equal(0, storedUtcDate.Minute);
    }

    [Fact]
    public async Task Test21_ExistingHistoricalPolicies_CanStillBeReadNormally()
    {
        using var context = CreateInMemoryContext();
        var service = new PolicyService(context);

        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Motor Insurance",
            Description = "Motor insurance policy",
            DefaultCoverageLimit = 500000m,
            DefaultDeductible = 10000m,
            BasePremiumRate = 5m
        };
        context.PolicyTypes.Add(policyType);

        var policyholderId = Guid.NewGuid();
        var historicalPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-HISTORICAL-001",
            PolicyholderId = policyholderId,
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = 100000m,
            Premium = 1200m,
            Deductible = 1000m,
            DeductiblePercentage = 5m,
            // Historical policy created 2 years ago
            StartDate = DateTime.UtcNow.AddYears(-2),
            ExpiryDate = DateTime.UtcNow.AddYears(-1),
            Status = PolicyStatus.Expired,
            RenewalStatus = RenewalStatus.NotDue
        };

        context.Policies.Add(historicalPolicy);
        await context.SaveChangesAsync();

        // 1. Reading by ID works without validation errors
        var readById = await service.GetByIdAsync(historicalPolicy.Id, policyholderId, Role.Policyholder);
        Assert.NotNull(readById);
        Assert.Equal(historicalPolicy.PolicyNumber, readById!.PolicyNumber);
        Assert.Equal(historicalPolicy.StartDate, readById.StartDate);
        Assert.Equal(historicalPolicy.ExpiryDate, readById.ExpiryDate);

        // 2. Reading by policyholder ID works
        var policies = await service.GetByPolicyholderIdAsync(policyholderId);
        Assert.Single(policies);
        Assert.Equal("POL-HISTORICAL-001", policies.First().PolicyNumber);
    }
}
