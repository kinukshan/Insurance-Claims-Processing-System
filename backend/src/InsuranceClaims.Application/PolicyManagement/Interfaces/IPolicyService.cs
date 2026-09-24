using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.Application.PolicyManagement.Interfaces;

/// <summary>
/// Service interface for policy management operations.
/// </summary>
public interface IPolicyService
{
    Task<IEnumerable<PolicyDto>> GetAllAsync();
    Task<PolicyDto?> GetByIdAsync(Guid id);
    Task<PolicyDto?> GetByIdAsync(Guid id, Guid? requestingUserId, Role? userRole);
    Task<IEnumerable<PolicyDto>> GetByPolicyholderIdAsync(Guid policyholderId);
    Task<PolicyDto> CreateAsync(CreatePolicyDto dto);
    Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto);
    Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto, Guid? requestingUserId, Role? userRole);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> DeleteAsync(Guid id, Guid requestingUserId, Role userRole);
    Task<PremiumCalculationResultDto?> CalculatePremiumAsync(Guid policyId);
    Task<IEnumerable<PolicyCoverageDto>> GetCoverageAsync(Guid policyId);
    Task<PolicyRenewalResultDto> RenewPolicyAsync(Guid policyId);
    Task<IEnumerable<PolicyDto>> ValidateExpiryAsync();
}
