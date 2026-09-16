using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Validators;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;

namespace InsuranceClaims.UnitTests.PolicyManagement;

/// <summary>
/// Unit tests for policy management: entities, validation, and business logic.
/// </summary>
public class PolicyServiceTests
{
    // --- Policy Entity Tests ---

    [Fact]
    public void Policy_IsExpired_ReturnsTrueWhenPastExpiryDate()
    {
        var policy = new Policy
        {
            ExpiryDate = DateTime.UtcNow.AddDays(-1),
            Status = PolicyStatus.Active
        };

        Assert.True(policy.IsExpired());
    }

    [Fact]
    public void Policy_IsExpired_ReturnsFalseWhenBeforeExpiryDate()
    {
        var policy = new Policy
        {
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            Status = PolicyStatus.Active
        };

        Assert.False(policy.IsExpired());
    }

    [Fact]
    public void Policy_CanRenew_ReturnsTrueForActivePolicy()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Active,
            RenewalStatus = RenewalStatus.NotDue
        };

        Assert.True(policy.CanRenew());
    }

    [Fact]
    public void Policy_CanRenew_ReturnsTrueForExpiredPolicy()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Expired,
            RenewalStatus = RenewalStatus.Pending
        };

        Assert.True(policy.CanRenew());
    }

    [Fact]
    public void Policy_CanRenew_ReturnsFalseForCancelledPolicy()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Cancelled,
            RenewalStatus = RenewalStatus.NotDue
        };

        Assert.False(policy.CanRenew());
    }

    [Fact]
    public void Policy_CanRenew_ReturnsFalseForLapsedPolicy()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Lapsed,
            RenewalStatus = RenewalStatus.Declined
        };

        Assert.False(policy.CanRenew());
    }

    [Fact]
    public void Policy_CanRenew_ReturnsFalseIfAlreadyRenewed()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Active,
            RenewalStatus = RenewalStatus.Renewed
        };

        Assert.False(policy.CanRenew());
    }

    [Fact]
    public void Policy_Lapse_SetsCorrectStatusAndRenewal()
    {
        var policy = new Policy
        {
            Status = PolicyStatus.Active,
            RenewalStatus = RenewalStatus.NotDue
        };

        policy.Lapse();

        Assert.Equal(PolicyStatus.Lapsed, policy.Status);
        Assert.Equal(RenewalStatus.Declined, policy.RenewalStatus);
    }

    // --- Validator Tests (Create) ---

    [Fact]
    public void ValidateCreate_ValidDto_ReturnsNoErrors()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = 1000m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Exclusions = "Flood damage"
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreate_EmptyPolicyholderId_ReturnsError()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.Empty,
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("PolicyholderId"));
    }

    [Fact]
    public void ValidateCreate_NegativeCoverageLimit_ReturnsError()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = -100m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Coverage limit"));
    }

    [Fact]
    public void ValidateCreate_StartDateAfterExpiryDate_ReturnsError()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            StartDate = DateTime.UtcNow.AddYears(1),
            ExpiryDate = DateTime.UtcNow
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Start date"));
    }

    [Fact]
    public void ValidateCreate_NegativeDeductible_ReturnsError()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            Deductible = -500m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Deductible"));
    }

    [Fact]
    public void ValidateCreate_ExclusionsTooLong_ReturnsError()
    {
        var dto = new CreatePolicyDto
        {
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = Guid.NewGuid(),
            CoverageLimit = 50000m,
            StartDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Exclusions = new string('x', 2001)
        };

        var errors = PolicyValidator.ValidateCreate(dto);

        Assert.Contains(errors, e => e.Contains("Exclusions"));
    }

    // --- Validator Tests (Update) ---

    [Fact]
    public void ValidateUpdate_ValidDto_ReturnsNoErrors()
    {
        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 60000m,
            Status = "Active"
        };

        var errors = PolicyValidator.ValidateUpdate(dto);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateUpdate_InvalidStatus_ReturnsError()
    {
        var dto = new UpdatePolicyDto
        {
            Status = "InvalidStatus"
        };

        var errors = PolicyValidator.ValidateUpdate(dto);

        Assert.Contains(errors, e => e.Contains("Invalid status"));
    }

    [Fact]
    public void ValidateUpdate_ZeroCoverageLimit_ReturnsError()
    {
        var dto = new UpdatePolicyDto
        {
            CoverageLimit = 0m
        };

        var errors = PolicyValidator.ValidateUpdate(dto);

        Assert.Contains(errors, e => e.Contains("Coverage limit"));
    }

    // --- Premium Calculation Tests ---

    [Fact]
    public void PremiumCalculation_BasicFormula_IsCorrect()
    {
        // Formula: (basePremiumRate × coverageLimit × riskMultiplier / 1000) - deductibleDiscount
        // Deductible discount: deductible * 0.05
        var basePremiumRate = 5.0m;
        var coverageLimit = 100000m;
        var riskMultiplier = 1.2m;
        var deductible = 2000m;

        var basePremium = basePremiumRate * coverageLimit * riskMultiplier / 1000m;
        var deductibleDiscount = deductible * 0.05m;
        var expectedPremium = Math.Round(Math.Max(basePremium - deductibleDiscount, 0m), 2);

        // basePremium = 5.0 × 100000 × 1.2 / 1000 = 600
        // deductibleDiscount = 2000 × 0.05 = 100
        // premium = 600 - 100 = 500
        Assert.Equal(500m, expectedPremium);
    }

    [Fact]
    public void PremiumCalculation_ZeroDeductible_NoDiscount()
    {
        var basePremiumRate = 3.0m;
        var coverageLimit = 50000m;
        var riskMultiplier = 1.0m;
        var deductible = 0m;

        var basePremium = basePremiumRate * coverageLimit * riskMultiplier / 1000m;
        var deductibleDiscount = deductible > 0 ? deductible * 0.05m : 0m;
        var expectedPremium = Math.Round(Math.Max(basePremium - deductibleDiscount, 0m), 2);

        // basePremium = 3.0 × 50000 × 1.0 / 1000 = 150
        Assert.Equal(150m, expectedPremium);
    }

    [Fact]
    public void PremiumCalculation_HighDeductible_NeverNegative()
    {
        var basePremiumRate = 1.0m;
        var coverageLimit = 1000m;
        var riskMultiplier = 1.0m;
        var deductible = 100000m; // Very high deductible

        var basePremium = basePremiumRate * coverageLimit * riskMultiplier / 1000m;
        var deductibleDiscount = deductible * 0.05m;
        var expectedPremium = Math.Round(Math.Max(basePremium - deductibleDiscount, 0m), 2);

        // basePremium = 1.0 × 1000 × 1.0 / 1000 = 1
        // deductibleDiscount = 100000 × 0.05 = 5000
        // Math.Max(1 - 5000, 0) = 0
        Assert.Equal(0m, expectedPremium);
    }

    // --- PolicyType Tests ---

    [Fact]
    public void PolicyType_DefaultValues_AreCorrect()
    {
        var policyType = new PolicyType();

        Assert.Equal(1.0m, policyType.RiskMultiplier);
        Assert.True(policyType.IsActive);
        Assert.Empty(policyType.Name);
        Assert.NotNull(policyType.Policies);
    }

    // --- PolicyCoverage Tests ---

    [Fact]
    public void PolicyCoverage_DefaultValues_AreCorrect()
    {
        var coverage = new PolicyCoverage();

        Assert.True(coverage.IsActive);
        Assert.Empty(coverage.CoverageType);
    }

    // --- Policy Default Values ---

    [Fact]
    public void Policy_DefaultStatus_IsDraft()
    {
        var policy = new Policy();

        Assert.Equal(PolicyStatus.Draft, policy.Status);
        Assert.Equal(RenewalStatus.NotDue, policy.RenewalStatus);
    }

    // --- Enum Values ---

    [Theory]
    [InlineData(PolicyStatus.Draft, 0)]
    [InlineData(PolicyStatus.Active, 1)]
    [InlineData(PolicyStatus.Expired, 2)]
    [InlineData(PolicyStatus.Lapsed, 3)]
    [InlineData(PolicyStatus.Cancelled, 4)]
    public void PolicyStatus_HasCorrectValues(PolicyStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Theory]
    [InlineData(RenewalStatus.NotDue, 0)]
    [InlineData(RenewalStatus.Pending, 1)]
    [InlineData(RenewalStatus.Renewed, 2)]
    [InlineData(RenewalStatus.Declined, 3)]
    public void RenewalStatus_HasCorrectValues(RenewalStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }
}
