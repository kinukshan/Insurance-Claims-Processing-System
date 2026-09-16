using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;

namespace InsuranceClaims.Infrastructure.ExternalServices;

/// <summary>
/// Stub implementation of policy validation.
/// Returns placeholder data until Kaushikesh's Policy module is merged.
/// Does not duplicate Policy Management business rules.
/// </summary>
public class PolicyValidationService : IPolicyValidationService
{
    public Task<CoverageValidationResultDto> ValidateCoverageAsync(
        Guid policyId,
        string claimType,
        decimal claimedAmount)
    {
        // Stub: always returns valid until Policy module is integrated
        // TODO: Replace with real policy lookup after Kaushikesh's merge
        var result = new CoverageValidationResultDto(
            IsValid: true,
            IsCovered: true,
            CoverageLimit: 100000m,
            DeductibleAmount: 500m,
            CoverageType: claimType,
            Issues: new List<string>()
        );

        return Task.FromResult(result);
    }

    public Task<bool> IsPolicyActiveAsync(Guid policyId)
    {
        // Stub: always returns true until Policy module is integrated
        // TODO: Replace with real policy lookup after Kaushikesh's merge
        return Task.FromResult(true);
    }
}
