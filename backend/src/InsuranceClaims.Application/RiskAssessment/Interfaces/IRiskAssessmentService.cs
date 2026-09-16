using InsuranceClaims.Application.RiskAssessment.DTOs;

namespace InsuranceClaims.Application.RiskAssessment.Interfaces;

/// <summary>
/// Service interface for risk assessment business operations.
/// </summary>
public interface IRiskAssessmentService
{
    /// <summary>
    /// Run a risk assessment on a claim using deterministic rules and optionally AI analysis.
    /// AI recommendation does NOT automatically become a final decision.
    /// </summary>
    Task<RiskAssessmentDto> AssessClaimAsync(Guid claimId, AssessClaimRequest request);

    /// <summary>Get the latest risk assessment for a claim.</summary>
    Task<RiskAssessmentDto?> GetAssessmentAsync(Guid claimId);

    /// <summary>Get all claims that have been flagged with unresolved fraud flags.</summary>
    Task<IReadOnlyList<RiskAssessmentDto>> GetFlaggedClaimsAsync();

    /// <summary>Get fraud case history for a policyholder.</summary>
    Task<IReadOnlyList<FraudCaseDto>> GetHistoryAsync(Guid policyholderId);

    /// <summary>Escalate a risk assessment to a fraud case for manual investigation.</summary>
    Task<FraudCaseDto> EscalateAsync(Guid assessmentId, EscalateRequest request);

    /// <summary>Get all fraud flags for a specific claim.</summary>
    Task<IReadOnlyList<FraudFlagDto>> GetFlagsAsync(Guid claimId);

    /// <summary>Update an existing fraud case.</summary>
    Task<FraudCaseDto> UpdateFraudCaseAsync(Guid fraudCaseId, UpdateFraudCaseRequest request);

    /// <summary>
    /// Get policyholder-safe review status.
    /// Never exposes internal fraud scores, flags, or detection rules.
    /// </summary>
    Task<PolicyholderReviewStatusDto> GetPolicyholderStatusAsync(Guid claimId);
}
