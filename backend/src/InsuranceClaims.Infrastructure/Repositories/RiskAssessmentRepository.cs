using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for risk assessment data access using EF Core.
/// </summary>
public class RiskAssessmentRepository : IRiskAssessmentRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RiskAssessmentRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Claim?> GetClaimByIdAsync(Guid claimId)
    {
        return await _dbContext.Claims.FirstOrDefaultAsync(c => c.Id == claimId);
    }

    /// <inheritdoc />
    public async Task<bool> HasDuplicateClaimAsync(Guid claimId, Guid policyHolderId, string description, DateTime incidentDate)
    {
        return await _dbContext.Claims
            .AnyAsync(c => c.Id != claimId
                && c.PolicyHolderId == policyHolderId
                && c.Description == description
                && c.IncidentDate == incidentDate);
    }

    /// <inheritdoc />
    public async Task<int> GetRecentClaimCountAsync(Guid policyHolderId, int months)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);
        return await _dbContext.Claims
            .CountAsync(c => c.PolicyHolderId == policyHolderId && c.CreatedAt >= cutoff);
    }

    /// <inheritdoc />
    public async Task<Domain.RiskAssessment.RiskAssessment?> GetByIdAsync(Guid id)
    {
        return await _dbContext.RiskAssessments
            .Include(r => r.FraudFlags)
            .Include(r => r.FraudCase)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <inheritdoc />
    public async Task<Domain.RiskAssessment.RiskAssessment?> GetByClaimIdAsync(Guid claimId)
    {
        return await _dbContext.RiskAssessments
            .Include(r => r.FraudFlags)
            .Include(r => r.FraudCase)
            .OrderByDescending(r => r.AssessmentTimestamp)
            .FirstOrDefaultAsync(r => r.ClaimId == claimId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.RiskAssessment.RiskAssessment>> GetFlaggedAsync()
    {
        return await _dbContext.RiskAssessments
            .Include(r => r.FraudFlags)
            .Include(r => r.FraudCase)
            .Where(r => r.FraudFlags.Any(f => !f.IsResolved))
            .OrderByDescending(r => r.RiskScore)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.RiskAssessment.RiskAssessment>> GetAllAsync()
    {
        return await _dbContext.RiskAssessments
            .Include(r => r.FraudFlags)
            .Include(r => r.FraudCase)
            .OrderByDescending(r => r.AssessmentTimestamp)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudCase>> GetFraudCasesByPolicyholderAsync(Guid policyholderId)
    {
        return await _dbContext.FraudCases
            .Include(fc => fc.RiskAssessment)
            .Where(fc => fc.PolicyHolderId == policyholderId)
            .OrderByDescending(fc => fc.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudFlag>> GetFlagsByClaimIdAsync(Guid claimId)
    {
        return await _dbContext.FraudFlags
            .Where(f => f.ClaimId == claimId)
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<FraudCase?> GetFraudCaseByIdAsync(Guid id)
    {
        return await _dbContext.FraudCases
            .Include(fc => fc.RiskAssessment)
            .FirstOrDefaultAsync(fc => fc.Id == id);
    }

    /// <inheritdoc />
    public async Task<FraudCase?> GetFraudCaseByAssessmentIdAsync(Guid assessmentId)
    {
        return await _dbContext.FraudCases
            .FirstOrDefaultAsync(fc => fc.RiskAssessmentId == assessmentId);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsForClaimAsync(Guid claimId)
    {
        return await _dbContext.RiskAssessments
            .AnyAsync(r => r.ClaimId == claimId);
    }

    /// <inheritdoc />
    public async Task AddAsync(Domain.RiskAssessment.RiskAssessment assessment)
    {
        await _dbContext.RiskAssessments.AddAsync(assessment);
    }

    /// <inheritdoc />
    public async Task AddFraudCaseAsync(FraudCase fraudCase)
    {
        await _dbContext.FraudCases.AddAsync(fraudCase);
    }

    /// <inheritdoc />
    public Task UpdateFraudCaseAsync(FraudCase fraudCase)
    {
        _dbContext.FraudCases.Update(fraudCase);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
