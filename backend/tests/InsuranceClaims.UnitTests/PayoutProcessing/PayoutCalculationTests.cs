using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.UnitTests.PayoutProcessing;

/// <summary>
/// Tests for deterministic payout calculations.
/// </summary>
public class PayoutCalculationTests
{
    [Fact]
    public void CalculatePayout_StandardCase_ReturnsCorrectAmount()
    {
        // Arrange: Claim 15000, Coverage 50000, Deductible 500
        var payout = new Payout
        {
            ApprovedClaimAmount = 15000m,
            CoverageLimit = 50000m,
            Deductible = 500m
        };

        // Act
        payout.CalculatePayout();

        // Assert: min(15000, 50000) - 500 = 14500
        Assert.Equal(14500m, payout.ProposedPayout);
        Assert.Equal(14500m, payout.FinalPayout);
    }

    [Fact]
    public void CalculatePayout_CoverageCapEnforced_CapsAtCoverageLimit()
    {
        // Arrange: Claim exceeds coverage
        var payout = new Payout
        {
            ApprovedClaimAmount = 100000m,
            CoverageLimit = 50000m,
            Deductible = 1000m
        };

        // Act
        payout.CalculatePayout();

        // Assert: min(100000, 50000) - 1000 = 49000
        Assert.Equal(49000m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_DeductibleExceedsEligible_ReturnsZero()
    {
        // Arrange: Deductible >= eligible amount
        var payout = new Payout
        {
            ApprovedClaimAmount = 500m,
            CoverageLimit = 50000m,
            Deductible = 1000m
        };

        // Act
        payout.CalculatePayout();

        // Assert: max(0, 500 - 1000) = 0
        Assert.Equal(0m, payout.ProposedPayout);
        Assert.Equal(0m, payout.FinalPayout);
    }

    [Fact]
    public void CalculatePayout_ZeroClaim_ReturnsZero()
    {
        var payout = new Payout
        {
            ApprovedClaimAmount = 0m,
            CoverageLimit = 50000m,
            Deductible = 500m
        };

        payout.CalculatePayout();

        Assert.Equal(0m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_ZeroDeductible_ReturnsFullEligible()
    {
        var payout = new Payout
        {
            ApprovedClaimAmount = 10000m,
            CoverageLimit = 50000m,
            Deductible = 0m
        };

        payout.CalculatePayout();

        Assert.Equal(10000m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_ExactCoverageLimit_ReturnsLimitMinusDeductible()
    {
        var payout = new Payout
        {
            ApprovedClaimAmount = 50000m,
            CoverageLimit = 50000m,
            Deductible = 2000m
        };

        payout.CalculatePayout();

        Assert.Equal(48000m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_PartialPayout_ClaimBelowCoverage()
    {
        // Partial payout: claim 3000, coverage 10000, deductible 250
        var payout = new Payout
        {
            ApprovedClaimAmount = 3000m,
            CoverageLimit = 10000m,
            Deductible = 250m
        };

        payout.CalculatePayout();

        // min(3000, 10000) - 250 = 2750
        Assert.Equal(2750m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_NeverReturnsNegative()
    {
        // Large deductible relative to claim
        var payout = new Payout
        {
            ApprovedClaimAmount = 100m,
            CoverageLimit = 100m,
            Deductible = 9999m
        };

        payout.CalculatePayout();

        Assert.True(payout.ProposedPayout >= 0m);
        Assert.True(payout.FinalPayout >= 0m);
        Assert.Equal(0m, payout.ProposedPayout);
    }

    [Fact]
    public void CalculatePayout_DecimalPrecision_HandlesCorrectly()
    {
        var payout = new Payout
        {
            ApprovedClaimAmount = 15000.75m,
            CoverageLimit = 50000.50m,
            Deductible = 499.99m
        };

        payout.CalculatePayout();

        // min(15000.75, 50000.50) - 499.99 = 14500.76
        Assert.Equal(14500.76m, payout.ProposedPayout);
    }
}
