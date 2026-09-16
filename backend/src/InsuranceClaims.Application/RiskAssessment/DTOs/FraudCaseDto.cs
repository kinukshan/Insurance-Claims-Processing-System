using InsuranceClaims.Domain.RiskAssessment;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Response DTO for a fraud investigation case.
/// </summary>
public class FraudCaseDto
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid RiskAssessmentId { get; set; }
    public Guid PolicyHolderId { get; set; }
    public FraudCaseStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public FraudCasePriority Priority { get; set; }
    public string PriorityDisplay => Priority.ToString();
    public string? AssignedReviewer { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Maps a domain FraudCase entity to a DTO.
    /// </summary>
    public static FraudCaseDto FromEntity(FraudCase entity)
    {
        return new FraudCaseDto
        {
            Id = entity.Id,
            ClaimId = entity.ClaimId,
            RiskAssessmentId = entity.RiskAssessmentId,
            PolicyHolderId = entity.PolicyHolderId,
            Status = entity.Status,
            Priority = entity.Priority,
            AssignedReviewer = entity.AssignedReviewer,
            Notes = entity.Notes,
            Resolution = entity.Resolution,
            ClosedAt = entity.ClosedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
