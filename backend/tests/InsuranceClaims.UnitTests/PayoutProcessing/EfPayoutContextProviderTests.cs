using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

public class EfPayoutContextProviderTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EfPayoutContextProvider _provider;

    public EfPayoutContextProviderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"PayoutContextTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _provider = new EfPayoutContextProvider(_dbContext, NullLogger<EfPayoutContextProvider>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<(Claim claim, Policy policy)> SeedClaimWithPolicy(ClaimStatus claimStatus, decimal claimedAmount = 5000m, decimal coverageLimit = 20000m, decimal deductible = 500m)
    {
        var policyType = new PolicyType
        {
            Id = Guid.NewGuid(),
            Name = "Comprehensive Auto",
            Description = "Full motor coverage"
        };
        _dbContext.PolicyTypes.Add(policyType);

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = $"POL-{Guid.NewGuid():N}"[..12].ToUpper(),
            PolicyholderId = Guid.NewGuid(),
            PolicyTypeId = policyType.Id,
            PolicyType = policyType,
            CoverageLimit = coverageLimit,
            Deductible = deductible,
            Premium = 1200m,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            Status = PolicyStatus.Active
        };
        _dbContext.Policies.Add(policy);

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = policy.Id,
            Policy = policy,
            PolicyHolderId = policy.PolicyholderId,
            ClaimNumber = $"CLM-{Guid.NewGuid():N}"[..12].ToUpper(),
            ClaimType = ClaimType.Auto,
            Description = "Collision damage",
            ClaimedAmount = claimedAmount,
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            IncidentLocation = "Colombo",
            Status = claimStatus
        };
        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        return (claim, policy);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithSubmittedClaim_ReturnsContextWithCorrectFinancials()
    {
        // Arrange — Active test claim state (Document Verification and Risk Assessment completed but status remains Submitted)
        var (claim, policy) = await SeedClaimWithPolicy(ClaimStatus.Submitted, claimedAmount: 7500m, coverageLimit: 25000m, deductible: 500m);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(claim.Id, context.ClaimId);
        Assert.Equal(7500m, context.ApprovedClaimAmount);
        Assert.Equal(policy.Id, context.PolicyId);
        Assert.Equal(25000m, context.CoverageLimit);
        Assert.Equal(500m, context.Deductible);
        Assert.Equal("Comprehensive Auto", context.PolicyType);
        Assert.Equal("Auto", context.ClaimType);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithApprovedClaim_ReturnsContext()
    {
        // Arrange
        var (claim, policy) = await SeedClaimWithPolicy(ClaimStatus.Approved);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(claim.Id, context.ClaimId);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithUnderReviewClaim_ReturnsContext()
    {
        // Arrange
        var (claim, policy) = await SeedClaimWithPolicy(ClaimStatus.UnderReview);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.NotNull(context);
        Assert.Equal(claim.Id, context.ClaimId);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithWithdrawnClaim_ReturnsNull()
    {
        // Arrange — Withdrawn claims like CLM-20260921-0001 MUST remain blocked
        var (claim, _) = await SeedClaimWithPolicy(ClaimStatus.Withdrawn);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithDraftClaim_ReturnsNull()
    {
        // Arrange — Draft claims must not be eligible for payout
        var (claim, _) = await SeedClaimWithPolicy(ClaimStatus.Draft);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithRejectedClaim_ReturnsNull()
    {
        // Arrange — Rejected claims must not be eligible for payout
        var (claim, _) = await SeedClaimWithPolicy(ClaimStatus.Rejected);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithClosedClaim_ReturnsNull()
    {
        // Arrange — Closed claims must not be eligible for payout
        var (claim, _) = await SeedClaimWithPolicy(ClaimStatus.Closed);

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WithNonExistentClaim_ReturnsNull()
    {
        // Act
        var context = await _provider.GetPayoutContextAsync(Guid.NewGuid());

        // Assert
        Assert.Null(context);
    }

    [Fact]
    public async Task GetPayoutContextAsync_WhenClaimHasNoPolicy_ReturnsNull()
    {
        // Arrange
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(), // Reference to non-existent policy
            PolicyHolderId = Guid.NewGuid(),
            ClaimNumber = "CLM-NOPOLICY",
            ClaimType = ClaimType.Property,
            Description = "No policy claim",
            ClaimedAmount = 1000m,
            IncidentDate = DateTime.UtcNow.AddDays(-1),
            IncidentLocation = "Kandy",
            Status = ClaimStatus.Submitted
        };
        _dbContext.Claims.Add(claim);
        await _dbContext.SaveChangesAsync();

        // Act
        var context = await _provider.GetPayoutContextAsync(claim.Id);

        // Assert
        Assert.Null(context);
    }
}
