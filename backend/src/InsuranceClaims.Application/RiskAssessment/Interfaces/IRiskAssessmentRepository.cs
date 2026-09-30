using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.RiskAssessment;

namespace InsuranceClaims.Application.RiskAssessment.Interfaces;

/// <summary>
/// Repository interface for risk assessment data access.
/// </summary>
public interface IRiskAssessmentRepository
{
    /// <summary>Get a claim by its ID.</summary>
    Task<Claim?> GetClaimByIdAsync(Guid claimId);

    /// <summary>Check if another claim exists with matching description and incident date for the policyholder.</summary>
    Task<bool> HasDuplicateClaimAsync(Guid claimId, Guid policyHolderId, string description, DateTime incidentDate);

    /// <summary>Count claims submitted by the policyholder within the specified number of months.</summary>
    Task<int> GetRecentClaimCountAsync(Guid policyHolderId, int months);

    /// <summary>Get a risk assessment by its ID, including flags and fraud case.</summary>
    Task<Domain.RiskAssessment.RiskAssessment?> GetByIdAsync(Guid id);

    /// <summary>Get the latest risk assessment for a specific claim.</summary>
    Task<Domain.RiskAssessment.RiskAssessment?> GetByClaimIdAsync(Guid claimId);

    /// <summary>Get all risk assessments that have unresolved fraud flags.</summary>
    Task<IReadOnlyList<Domain.RiskAssessment.RiskAssessment>> GetFlaggedAsync();

    /// <summary>Get all risk assessments ordered by timestamp descending.</summary>
    Task<IReadOnlyList<Domain.RiskAssessment.RiskAssessment>> GetAllAsync();

    /// <summary>Get fraud case history for a policyholder.</summary>
    Task<IReadOnlyList<FraudCase>> GetFraudCasesByPolicyholderAsync(Guid policyholderId);

    /// <summary>Get all fraud flags for a specific claim.</summary>
    Task<IReadOnlyList<FraudFlag>> GetFlagsByClaimIdAsync(Guid claimId);

    /// <summary>Get a fraud case by its ID.</summary>
    Task<FraudCase?> GetFraudCaseByIdAsync(Guid id);

    /// <summary>Get a fraud case by risk assessment ID.</summary>
    Task<FraudCase?> GetFraudCaseByAssessmentIdAsync(Guid assessmentId);

    /// <summary>Check if a claim already has an assessment.</summary>
    Task<bool> ExistsForClaimAsync(Guid claimId);

    /// <summary>Add a new risk assessment.</summary>
    Task AddAsync(Domain.RiskAssessment.RiskAssessment assessment);

    /// <summary>Add a fraud case.</summary>
    Task AddFraudCaseAsync(FraudCase fraudCase);

    /// <summary>Update an existing fraud case.</summary>
    Task UpdateFraudCaseAsync(FraudCase fraudCase);

    /// <summary>Persist changes to the database.</summary>
    Task SaveChangesAsync();
}
