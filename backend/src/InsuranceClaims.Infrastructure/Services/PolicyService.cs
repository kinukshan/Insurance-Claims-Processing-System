using Microsoft.EntityFrameworkCore;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Application.PolicyManagement.Validators;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Infrastructure.Persistence;

namespace InsuranceClaims.Infrastructure.Services;

/// <summary>
/// Service implementation for policy management operations.
/// Placed in Infrastructure layer since it depends on ApplicationDbContext/EF Core.
/// </summary>
public class PolicyService : IPolicyService
{
    private readonly ApplicationDbContext _context;

    public PolicyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PolicyDto>> GetAllAsync()
    {
        var policies = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return policies.Select(MapToDto);
    }

    public async Task<PolicyDto?> GetByIdAsync(Guid id)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        return policy == null ? null : MapToDto(policy);
    }

    public async Task<IEnumerable<PolicyDto>> GetByPolicyholderIdAsync(Guid policyholderId)
    {
        var policies = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .AsNoTracking()
            .Where(p => p.PolicyholderId == policyholderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return policies.Select(MapToDto);
    }

    public async Task<PolicyDto> CreateAsync(CreatePolicyDto dto)
    {
        var errors = PolicyValidator.ValidateCreate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        // Verify the policy type exists
        var policyType = await _context.PolicyTypes.FindAsync(dto.PolicyTypeId);
        if (policyType == null)
            throw new ArgumentException($"Policy type with ID '{dto.PolicyTypeId}' not found.");

        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = GeneratePolicyNumber(),
            PolicyholderId = dto.PolicyholderId,
            PolicyTypeId = dto.PolicyTypeId,
            CoverageLimit = dto.CoverageLimit,
            Deductible = dto.Deductible,
            StartDate = dto.StartDate,
            ExpiryDate = dto.ExpiryDate,
            Exclusions = dto.Exclusions,
            Status = PolicyStatus.Draft,
            RenewalStatus = RenewalStatus.NotDue
        };

        // Calculate premium using deterministic logic
        policy.Premium = CalculatePremium(policy, policyType);

        _context.Policies.Add(policy);
        await _context.SaveChangesAsync();

        // Re-load with navigation properties
        var created = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .AsNoTracking()
            .FirstAsync(p => p.Id == policy.Id);

        return MapToDto(created);
    }

    public async Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto)
    {
        var errors = PolicyValidator.ValidateUpdate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
            return null;

        // Apply updates
        if (dto.CoverageLimit.HasValue)
            policy.CoverageLimit = dto.CoverageLimit.Value;

        if (dto.Deductible.HasValue)
            policy.Deductible = dto.Deductible.Value;

        if (dto.ExpiryDate.HasValue)
        {
            if (dto.ExpiryDate.Value <= policy.StartDate)
                throw new ArgumentException("Expiry date must be after start date.");
            policy.ExpiryDate = dto.ExpiryDate.Value;
        }

        if (dto.Exclusions != null)
            policy.Exclusions = dto.Exclusions;

        if (dto.Status != null)
        {
            if (Enum.TryParse<PolicyStatus>(dto.Status, true, out var newStatus))
                policy.Status = newStatus;
            else
                throw new ArgumentException($"Invalid status '{dto.Status}'.");
        }

        // Recalculate premium if coverage/deductible changed
        if (dto.CoverageLimit.HasValue || dto.Deductible.HasValue)
        {
            var policyType = policy.PolicyType ?? await _context.PolicyTypes.FindAsync(policy.PolicyTypeId);
            if (policyType != null)
                policy.Premium = CalculatePremium(policy, policyType);
        }

        await _context.SaveChangesAsync();

        return MapToDto(policy);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var policy = await _context.Policies.FindAsync(id);
        if (policy == null)
            return false;

        // Only allow deletion of draft policies — historical records are preserved
        if (policy.Status != PolicyStatus.Draft)
            throw new InvalidOperationException(
                $"Cannot delete a policy with status '{policy.Status}'. Only draft policies can be deleted.");

        _context.Policies.Remove(policy);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PremiumCalculationResultDto?> CalculatePremiumAsync(Guid policyId)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null || policy.PolicyType == null)
            return null;

        var basePremiumRate = policy.PolicyType.BasePremiumRate;
        var riskMultiplier = policy.PolicyType.RiskMultiplier;
        var deductibleDiscount = policy.Deductible > 0
            ? Math.Round(policy.Deductible * 0.05m, 2)
            : 0m;

        var calculatedPremium = CalculatePremium(policy, policy.PolicyType);

        return new PremiumCalculationResultDto
        {
            PolicyId = policy.Id,
            PolicyNumber = policy.PolicyNumber,
            BasePremiumRate = basePremiumRate,
            CoverageLimit = policy.CoverageLimit,
            RiskMultiplier = riskMultiplier,
            DeductibleDiscount = deductibleDiscount,
            CalculatedPremium = calculatedPremium,
            Breakdown = $"({basePremiumRate} × {policy.CoverageLimit} × {riskMultiplier} / 1000) - {deductibleDiscount} deductible discount = {calculatedPremium}"
        };
    }

    public async Task<IEnumerable<PolicyCoverageDto>> GetCoverageAsync(Guid policyId)
    {
        var policyExists = await _context.Policies.AnyAsync(p => p.Id == policyId);
        if (!policyExists)
            return Enumerable.Empty<PolicyCoverageDto>();

        var coverages = await _context.PolicyCoverages
            .AsNoTracking()
            .Where(c => c.PolicyId == policyId)
            .OrderBy(c => c.CoverageType)
            .ToListAsync();

        return coverages.Select(c => new PolicyCoverageDto
        {
            Id = c.Id,
            PolicyId = c.PolicyId,
            CoverageType = c.CoverageType,
            Description = c.Description,
            CoverageLimit = c.CoverageLimit,
            DeductibleAmount = c.DeductibleAmount,
            PercentageOfCoverage = c.PercentageOfCoverage,
            IsActive = c.IsActive
        });
    }

    public async Task<PolicyRenewalResultDto> RenewPolicyAsync(Guid policyId)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .FirstOrDefaultAsync(p => p.Id == policyId);

        if (policy == null)
            return new PolicyRenewalResultDto
            {
                Success = false,
                Message = "Policy not found."
            };

        if (!policy.CanRenew())
            return new PolicyRenewalResultDto
            {
                Success = false,
                Message = $"Policy cannot be renewed. Current status: {policy.Status}, Renewal status: {policy.RenewalStatus}.",
                OriginalPolicyId = policy.Id
            };

        // Calculate the duration of the original policy to determine renewal period
        var duration = policy.ExpiryDate - policy.StartDate;
        var newStartDate = policy.ExpiryDate;
        var newExpiryDate = newStartDate + duration;

        // Create renewed policy
        var renewedPolicy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = GeneratePolicyNumber(),
            PolicyholderId = policy.PolicyholderId,
            PolicyTypeId = policy.PolicyTypeId,
            CoverageLimit = policy.CoverageLimit,
            Deductible = policy.Deductible,
            StartDate = newStartDate,
            ExpiryDate = newExpiryDate,
            Exclusions = policy.Exclusions,
            Status = PolicyStatus.Active,
            RenewalStatus = RenewalStatus.NotDue
        };

        // Calculate premium for renewed policy
        var policyType = policy.PolicyType ?? await _context.PolicyTypes.FindAsync(policy.PolicyTypeId);
        if (policyType != null)
            renewedPolicy.Premium = CalculatePremium(renewedPolicy, policyType);

        // Copy coverages to new policy
        foreach (var coverage in policy.Coverages)
        {
            renewedPolicy.Coverages.Add(new PolicyCoverage
            {
                Id = Guid.NewGuid(),
                PolicyId = renewedPolicy.Id,
                CoverageType = coverage.CoverageType,
                Description = coverage.Description,
                CoverageLimit = coverage.CoverageLimit,
                DeductibleAmount = coverage.DeductibleAmount,
                PercentageOfCoverage = coverage.PercentageOfCoverage,
                IsActive = coverage.IsActive
            });
        }

        // Mark original policy as renewed
        policy.RenewalStatus = RenewalStatus.Renewed;

        _context.Policies.Add(renewedPolicy);
        await _context.SaveChangesAsync();

        return new PolicyRenewalResultDto
        {
            Success = true,
            Message = "Policy renewed successfully.",
            OriginalPolicyId = policy.Id,
            RenewedPolicyId = renewedPolicy.Id,
            RenewedPolicyNumber = renewedPolicy.PolicyNumber,
            NewStartDate = renewedPolicy.StartDate,
            NewExpiryDate = renewedPolicy.ExpiryDate,
            NewPremium = renewedPolicy.Premium
        };
    }

    public async Task<IEnumerable<PolicyDto>> ValidateExpiryAsync()
    {
        var now = DateTime.UtcNow;
        var expiredPolicies = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .Where(p => p.Status == PolicyStatus.Active && p.ExpiryDate <= now)
            .ToListAsync();

        // Update expired policies
        foreach (var policy in expiredPolicies)
        {
            policy.Status = PolicyStatus.Expired;
            policy.RenewalStatus = RenewalStatus.Pending;
        }

        if (expiredPolicies.Count > 0)
            await _context.SaveChangesAsync();

        return expiredPolicies.Select(MapToDto);
    }

    // --- Private helpers ---

    /// <summary>
    /// Deterministic premium calculation:
    /// (basePremiumRate × coverageLimit × riskMultiplier / 1000) - deductible discount
    /// </summary>
    private static decimal CalculatePremium(Policy policy, PolicyType policyType)
    {
        var basePremium = policyType.BasePremiumRate * policy.CoverageLimit * policyType.RiskMultiplier / 1000m;
        var deductibleDiscount = policy.Deductible > 0
            ? policy.Deductible * 0.05m
            : 0m;
        var premium = Math.Max(basePremium - deductibleDiscount, 0m);
        return Math.Round(premium, 2);
    }

    /// <summary>
    /// Generates a unique policy number in the format POL-XXXXXXXX.
    /// </summary>
    private static string GeneratePolicyNumber()
    {
        return $"POL-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private static PolicyDto MapToDto(Policy policy)
    {
        return new PolicyDto
        {
            Id = policy.Id,
            PolicyNumber = policy.PolicyNumber,
            PolicyholderId = policy.PolicyholderId,
            PolicyTypeId = policy.PolicyTypeId,
            PolicyTypeName = policy.PolicyType?.Name ?? string.Empty,
            CoverageLimit = policy.CoverageLimit,
            Premium = policy.Premium,
            Deductible = policy.Deductible,
            StartDate = policy.StartDate,
            ExpiryDate = policy.ExpiryDate,
            Status = policy.Status.ToString(),
            RenewalStatus = policy.RenewalStatus.ToString(),
            Exclusions = policy.Exclusions,
            IsExpired = policy.IsExpired(),
            CanRenew = policy.CanRenew(),
            Coverages = policy.Coverages.Select(c => new PolicyCoverageDto
            {
                Id = c.Id,
                PolicyId = c.PolicyId,
                CoverageType = c.CoverageType,
                Description = c.Description,
                CoverageLimit = c.CoverageLimit,
                DeductibleAmount = c.DeductibleAmount,
                PercentageOfCoverage = c.PercentageOfCoverage,
                IsActive = c.IsActive
            }).ToList(),
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }
}
