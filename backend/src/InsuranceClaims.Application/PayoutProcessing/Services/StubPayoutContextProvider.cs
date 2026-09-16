using InsuranceClaims.Application.PayoutProcessing.Interfaces;

namespace InsuranceClaims.Application.PayoutProcessing.Services;

/// <summary>
/// Development stub for IPayoutContextProvider.
///
/// Returns deterministic sample data during independent Payout module development.
/// Will be replaced by a real implementation that queries the Claims and Policy
/// modules via EF Core once Kaushikesh's (Policy) and Arulkumaran's (Claims)
/// components are integrated.
///
/// This stub is ONLY for independent development and testing.
/// It is NOT exposed as a production API input.
/// </summary>
public class StubPayoutContextProvider : IPayoutContextProvider
{
    /// <inheritdoc />
    public Task<PayoutContext?> GetPayoutContextAsync(Guid claimId)
    {
        // Return deterministic test data for any claim ID.
        // In the real implementation, this would:
        //   1. Retrieve the Claim by ID (Arulkumaran's module)
        //   2. Verify the claim is in an Approved status
        //   3. Retrieve the Policy and PolicyCoverage (Kaushikesh's module)
        //   4. Extract CoverageLimit and Deductible from the policy

        var context = new PayoutContext
        {
            ClaimId = claimId,
            ApprovedClaimAmount = 15000.00m,
            PolicyId = Guid.NewGuid(),
            PolicyType = "Comprehensive",
            ClaimType = "Vehicle Damage",
            CoverageLimit = 50000.00m,
            Deductible = 500.00m
        };

        return Task.FromResult<PayoutContext?>(context);
    }
}
