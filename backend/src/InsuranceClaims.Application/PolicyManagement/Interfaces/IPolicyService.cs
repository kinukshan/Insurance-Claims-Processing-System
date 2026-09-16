using InsuranceClaims.Application.PolicyManagement.DTOs;

namespace InsuranceClaims.Application.PolicyManagement.Interfaces;

/// <summary>
/// Service interface for policy management operations.
/// </summary>
public interface IPolicyService
{
    Task<IEnumerable<PolicyDto>> GetAllAsync();
    Task<PolicyDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<PolicyDto>> GetByPolicyholderIdAsync(Guid policyholderId);
    Task<PolicyDto> CreateAsync(CreatePolicyDto dto);
    Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<PremiumCalculationResultDto?> CalculatePremiumAsync(Guid policyId);
    Task<IEnumerable<PolicyCoverageDto>> GetCoverageAsync(Guid policyId);
    Task<PolicyRenewalResultDto> RenewPolicyAsync(Guid policyId);
    Task<IEnumerable<PolicyDto>> ValidateExpiryAsync();
}
