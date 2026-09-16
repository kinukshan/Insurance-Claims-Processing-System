namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Policyholder-safe review status DTO.
/// Exposes ONLY safe statuses — no internal fraud scores, flags, or detection rules.
/// </summary>
public class PolicyholderReviewStatusDto
{
    public Guid ClaimId { get; set; }

    /// <summary>
    /// Safe display status. One of:
    /// - "Additional Review Required"
    /// - "Under Manual Review"
    /// - "Review Completed"
    /// </summary>
    public string ReviewStatus { get; set; } = string.Empty;

    public DateTime? LastUpdated { get; set; }
}
