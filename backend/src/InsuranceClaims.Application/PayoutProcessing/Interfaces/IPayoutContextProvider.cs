namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Provides trusted backend data required for payout calculations.
///
/// This abstraction decouples the Payout module from the Policy and Claims modules
/// during independent development. The real implementation will retrieve data from
/// EF Core / the Policy and Claims services once those modules are integrated.
///
/// IMPORTANT: CoverageLimit, Deductible, and ApprovedClaimAmount must NEVER come
/// from client-supplied API inputs. They must be retrieved from the authoritative
/// backend sources via this interface.
/// </summary>
public interface IPayoutContextProvider
{
    /// <summary>
    /// Retrieve all trusted context needed to calculate a payout for a given claim.
    /// </summary>
    /// <param name="claimId">The claim ID to retrieve context for.</param>
    /// <returns>The payout context, or null if the claim is not found or not eligible.</returns>
    Task<PayoutContext?> GetPayoutContextAsync(Guid claimId);
}

/// <summary>
/// Trusted payout calculation context — all values from authoritative backend data.
/// </summary>
public record PayoutContext
{
    /// <summary>Claim identifier.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>The approved claim amount (from Claims module).</summary>
    public required decimal ApprovedClaimAmount { get; init; }

    /// <summary>Policy identifier (from Claims/Policy modules).</summary>
    public required Guid PolicyId { get; init; }

    /// <summary>Policy type name (from Policy module).</summary>
    public required string PolicyType { get; init; }

    /// <summary>Claim type description (from Claims module).</summary>
    public required string ClaimType { get; init; }

    /// <summary>Maximum coverage limit (from Policy module).</summary>
    public required decimal CoverageLimit { get; init; }

    /// <summary>Deductible amount (from Policy module).</summary>
    public required decimal Deductible { get; init; }
}
