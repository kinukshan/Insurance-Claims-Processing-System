using InsuranceClaims.Application.ClaimsManagement.DTOs;

namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Contract for policy validation operations.
/// This interface abstracts the Policy module dependency so Claims can
/// validate coverage without directly depending on Policy implementation.
/// 
/// Kinukshan/Kaushikesh will provide the real implementation after merge.
/// </summary>
public interface IPolicyValidationService
{
    /// <summary>
    /// Validates whether a claim is covered by its associated policy.
    /// </summary>
    Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid policyId, string claimType, decimal claimedAmount);

    /// <summary>
    /// Checks whether a policy exists and is active.
    /// </summary>
    Task<bool> IsPolicyActiveAsync(Guid policyId);

    /// <summary>
    /// Checks whether a policy belongs to the specified policyholder.
    /// </summary>
    Task<bool> ValidatePolicyOwnershipAsync(Guid policyId, Guid policyHolderId);

    /// <summary>
    /// Retrieves the policyholder ID for a policy, or null if the policy does not exist.
    /// </summary>
    Task<Guid?> GetPolicyOwnerIdAsync(Guid policyId);

    /// <summary>
    /// Retrieves policy validation details including ownership and policy type navigation.
    /// </summary>
    Task<PolicyValidationDetailsDto?> GetPolicyDetailsAsync(Guid policyId);
}

/// <summary>
/// Essential policy details required for claims validation.
/// </summary>
public record PolicyValidationDetailsDto(
    Guid PolicyId,
    Guid PolicyholderId,
    Guid PolicyTypeId,
    string? PolicyTypeName,
    bool IsActive
);
