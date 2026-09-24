using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.ExternalServices;

/// <summary>
/// Implementation of policy validation operations.
/// Interacts with Policy entities via ApplicationDbContext.
/// </summary>
public class PolicyValidationService : IPolicyValidationService
{
    private readonly ApplicationDbContext _context;

    public PolicyValidationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CoverageValidationResultDto> ValidateCoverageAsync(
        Guid policyId,
        string claimType,
        decimal claimedAmount)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null)
        {
            return new CoverageValidationResultDto(
                IsValid: false,
                IsCovered: false,
                CoverageLimit: 0m,
                DeductibleAmount: 0m,
                CoverageType: claimType,
                Issues: new List<string> { $"Policy with ID '{policyId}' not found." }
            );
        }

        var policyTypeName = policy.PolicyType?.Name;
        var issues = new List<string>();

        if (!PolicyClaimCompatibility.IsCompatible(policyTypeName, claimType))
        {
            issues.Add(PolicyClaimCompatibility.GetErrorMessage(policyTypeName, claimType));
            return new CoverageValidationResultDto(
                IsValid: false,
                IsCovered: false,
                CoverageLimit: policy.CoverageLimit,
                DeductibleAmount: policy.Deductible,
                CoverageType: claimType,
                Issues: issues
            );
        }

        if (policy.Status != PolicyStatus.Active)
        {
            issues.Add($"Policy is not active (Status: {policy.Status}).");
        }

        if (policy.IsExpired())
        {
            issues.Add("Policy has expired.");
        }

        if (claimedAmount > policy.CoverageLimit)
        {
            issues.Add($"Claimed amount exceeds policy coverage limit of {policy.CoverageLimit:C}.");
        }

        var isValid = issues.Count == 0;
        var isCovered = isValid;

        return new CoverageValidationResultDto(
            IsValid: isValid,
            IsCovered: isCovered,
            CoverageLimit: policy.CoverageLimit,
            DeductibleAmount: policy.Deductible,
            CoverageType: claimType,
            Issues: issues
        );
    }

    public async Task<bool> IsPolicyActiveAsync(Guid policyId)
    {
        var policy = await _context.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == policyId);
        return policy != null && policy.Status == PolicyStatus.Active && !policy.IsExpired();
    }

    public async Task<bool> ValidatePolicyOwnershipAsync(Guid policyId, Guid policyHolderId)
    {
        return await _context.Policies.AsNoTracking().AnyAsync(p => p.Id == policyId && p.PolicyholderId == policyHolderId);
    }

    public async Task<Guid?> GetPolicyOwnerIdAsync(Guid policyId)
    {
        var policy = await _context.Policies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == policyId);
        return policy?.PolicyholderId;
    }

    public async Task<PolicyValidationDetailsDto?> GetPolicyDetailsAsync(Guid policyId)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null) return null;

        return new PolicyValidationDetailsDto(
            policy.Id,
            policy.PolicyholderId,
            policy.PolicyTypeId,
            policy.PolicyType?.Name,
            policy.Status == PolicyStatus.Active && !policy.IsExpired()
        );
    }
}
