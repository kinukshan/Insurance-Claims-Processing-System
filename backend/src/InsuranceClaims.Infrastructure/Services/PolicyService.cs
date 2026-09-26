using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InsuranceClaims.Application.Common.Exceptions;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Application.PolicyManagement.Validators;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Enums;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;

namespace InsuranceClaims.Infrastructure.Services;

/// <summary>
/// Service implementation for policy management operations.
/// Placed in Infrastructure layer since it depends on ApplicationDbContext/EF Core.
/// </summary>
public class PolicyService : IPolicyService
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.Extensions.Logging.ILogger<PolicyService>? _logger;

    public PolicyService(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger<PolicyService>? logger = null)
    {
        _context = context;
        _logger = logger;
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

    public Task<PolicyDto?> GetByIdAsync(Guid id) =>
        GetByIdAsync(id, null, null);

    public async Task<PolicyDto?> GetByIdAsync(Guid id, Guid? requestingUserId, Role? userRole)
    {
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
            return null;

        if (userRole == Role.Policyholder && requestingUserId.HasValue && policy.PolicyholderId != requestingUserId.Value)
            throw new UnauthorizedAccessException("You do not have permission to view this policy.");

        return MapToDto(policy);
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
        // Validate input
        var errors = PolicyValidator.ValidateCreate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join("; ", errors));

        // Fetch policy type to calculate premium
        var policyType = await _context.PolicyTypes.FindAsync(dto.PolicyTypeId);
        if (policyType == null)
            throw new ArgumentException($"Policy type with ID '{dto.PolicyTypeId}' not found.");

        // Identify fixed deductible from authoritative backend data
        var fixedDeductible = PolicyClaimCompatibility.GetFixedDeductible(policyType.Id)
            ?? PolicyClaimCompatibility.GetFixedDeductible(policyType.Name);

        if (!fixedDeductible.HasValue)
        {
            throw new ArgumentException($"Unsupported policy type '{policyType.Name}'.");
        }

        // Always enforce authoritative fixed deductible (ignoring any client-submitted deductible)
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = GeneratePolicyNumber(),
            PolicyholderId = dto.PolicyholderId,
            PolicyTypeId = dto.PolicyTypeId,
            CoverageLimit = dto.CoverageLimit,
            Deductible = fixedDeductible.Value,
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

    public Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto) =>
        UpdateAsync(id, dto, null, null);

    public async Task<PolicyDto?> UpdateAsync(Guid id, UpdatePolicyDto dto, Guid? requestingUserId, Role? userRole)
    {
        // 1. Role-based authorization: only Underwriters and Admins (or system/internal calls without role) can edit
        if (userRole == Role.Policyholder)
            throw new UnauthorizedAccessException("Policyholders do not have permission to update policies.");

        if (userRole == Role.ClaimsAdjuster)
            throw new UnauthorizedAccessException("Claims adjusters do not have permission to update policies.");

        if (userRole.HasValue && userRole != Role.Underwriter && userRole != Role.Admin)
            throw new UnauthorizedAccessException("Only Underwriters and Admins are authorized to update policies.");

        // 2. Validate update payload
        var errors = PolicyValidator.ValidateUpdate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        // 3. Find existing policy
        var policy = await _context.Policies
            .Include(p => p.PolicyType)
            .Include(p => p.Coverages)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null)
            return null;

        // 4. Role-specific field restriction: Underwriter cannot change policy status
        if (dto.Status != null)
        {
            if (userRole.HasValue && userRole != Role.Admin)
            {
                if (!string.Equals(dto.Status, policy.Status.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Underwriters do not have permission to change policy status.");
            }
        }

        // 5. Validated status transitions (Admin or internal operations)
        if (dto.Status != null && (!userRole.HasValue || userRole == Role.Admin))
        {
            if (!Enum.TryParse<PolicyStatus>(dto.Status, true, out var newStatus))
                throw new ArgumentException($"Invalid status '{dto.Status}'.");

            if (newStatus != policy.Status)
            {
                if (policy.Status == PolicyStatus.Cancelled)
                    throw new ArgumentException("Cannot transition a cancelled policy to any other status. Cancelled is a terminal state.");

                var isValidTransition = policy.Status switch
                {
                    PolicyStatus.Draft => newStatus == PolicyStatus.Active || newStatus == PolicyStatus.Cancelled,
                    PolicyStatus.Active => newStatus == PolicyStatus.Expired || newStatus == PolicyStatus.Lapsed || newStatus == PolicyStatus.Cancelled,
                    PolicyStatus.Expired => newStatus == PolicyStatus.Active || newStatus == PolicyStatus.Lapsed || newStatus == PolicyStatus.Cancelled,
                    PolicyStatus.Lapsed => newStatus == PolicyStatus.Cancelled,
                    _ => false
                };

                if (!isValidTransition)
                    throw new ArgumentException($"Cannot transition policy from '{policy.Status}' to '{newStatus}'.");

                policy.Status = newStatus;
            }
        }

        // 6. Coverage limit validation and update
        if (dto.CoverageLimit.HasValue)
        {
            if (dto.CoverageLimit.Value <= 0)
                throw new ArgumentException("Coverage limit must be greater than zero.");
            policy.CoverageLimit = dto.CoverageLimit.Value;
        }

        // 7. Deductible protection & PolicyType resolution
        // PolicyService.UpdateAsync must NOT assume policy.PolicyType is already loaded.
        var policyType = policy.PolicyType ?? await _context.PolicyTypes.FindAsync(policy.PolicyTypeId);
        var isLife = policyType != null && (policyType.Id == PolicyClaimCompatibility.LifeInsuranceId || PolicyClaimCompatibility.IsLifeInsurance(policyType.Name));

        // Existing policy agreed deductibles are preserved; Life Insurance strictly enforces 0m.
        // General policy edit requests cannot bypass the fixed-deductible rule or alter agreed terms.
        if (isLife)
        {
            policy.Deductible = 0m;
        }
        // Non-Life existing policy deductibles remain untouched (dto.Deductible is ignored).

        // 8. Expiry date validation and update
        if (dto.ExpiryDate.HasValue)
        {
            if (dto.ExpiryDate.Value <= policy.StartDate)
                throw new ArgumentException("Expiry date must be after start date.");
            policy.ExpiryDate = dto.ExpiryDate.Value;
        }

        // 9. Exclusions update
        if (dto.Exclusions != null)
            policy.Exclusions = dto.Exclusions;

        // 10. Recalculate premium only if coverage limit changed
        if (dto.CoverageLimit.HasValue)
        {
            if (policyType != null)
                policy.Premium = CalculatePremium(policy, policyType);
        }

        // 11. Policy audit logging
        _logger?.LogInformation(
            "Policy {PolicyId} updated by User {UserId} (Role: {Role}). CoverageLimit={CoverageLimit}, ExpiryDate={ExpiryDate}, Status={Status}",
            policy.Id, requestingUserId, userRole, policy.CoverageLimit, policy.ExpiryDate, policy.Status);

        await _context.SaveChangesAsync();

        return MapToDto(policy);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var hasClaims = await _context.Claims.AnyAsync(c => c.PolicyId == id);
        if (hasClaims)
            throw new ConflictException("This policy cannot be deleted because claims already exist.");

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

    public async Task<bool> DeleteAsync(Guid id, Guid requestingUserId, Role userRole)
    {
        // ClaimsAdjuster has no policy deletion permissions
        if (userRole == Role.ClaimsAdjuster)
            throw new UnauthorizedAccessException("Claims adjusters do not have permission to delete policies.");

        var policy = await _context.Policies.FindAsync(id);
        if (policy == null)
            return false;

        // Policyholder may delete only their own draft policy
        if (userRole == Role.Policyholder && policy.PolicyholderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to delete this policy.");

        // Underwriter and Admin are allowed for draft policies

        // Only allow deletion of draft policies
        if (policy.Status != PolicyStatus.Draft)
            throw new InvalidOperationException(
                $"Cannot delete a policy with status '{policy.Status}'. Only draft policies can be deleted.");

        // If ANY claim references the policy: return 409 Conflict
        var hasClaims = await _context.Claims.AnyAsync(c => c.PolicyId == id);
        if (hasClaims)
            throw new ConflictException("This policy cannot be deleted because claims already exist.");

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
    /// PROJECT BUSINESS RULE: Simplified project premium calculation; not an actuarial Life Insurance pricing model.
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
        var insuranceClass = policy.PolicyType?.InsuranceClass ?? InsuranceClass.General;
        var insuranceClassCode = policy.PolicyType != null ? policy.PolicyType.InsuranceClass.ToString() : "General";
        var insuranceClassName = policy.PolicyType != null
            ? (policy.PolicyType.InsuranceClass == InsuranceClass.LongTerm ? "Long-Term Insurance" : "General Insurance")
            : "General Insurance";

        return new PolicyDto
        {
            Id = policy.Id,
            PolicyNumber = policy.PolicyNumber,
            PolicyholderId = policy.PolicyholderId,
            PolicyTypeId = policy.PolicyTypeId,
            PolicyTypeName = policy.PolicyType?.Name ?? string.Empty,
            InsuranceClass = (int)insuranceClass,
            InsuranceClassCode = insuranceClassCode,
            InsuranceClassName = insuranceClassName,
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
