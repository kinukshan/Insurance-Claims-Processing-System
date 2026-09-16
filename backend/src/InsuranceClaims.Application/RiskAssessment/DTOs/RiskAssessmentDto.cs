using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Response DTO for a risk assessment result.
/// </summary>
public class RiskAssessmentDto
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public decimal RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string RiskLevelDisplay => RiskLevel.ToString();
    public RiskRecommendation Recommendation { get; set; }
    public string RecommendationDisplay => Recommendation.ToString();
    public AssessorType AssessorType { get; set; }
    public DateTime AssessmentTimestamp { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int FraudFlagCount { get; set; }
    public bool HasFraudCase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Maps a domain RiskAssessment entity to a DTO.
    /// </summary>
    public static RiskAssessmentDto FromEntity(Domain.RiskAssessment.RiskAssessment entity)
    {
        return new RiskAssessmentDto
        {
            Id = entity.Id,
            ClaimId = entity.ClaimId,
            RiskScore = entity.RiskScore,
            RiskLevel = entity.RiskLevel,
            Recommendation = entity.Recommendation,
            AssessorType = entity.AssessorType,
            AssessmentTimestamp = entity.AssessmentTimestamp,
            Summary = entity.Summary,
            FraudFlagCount = entity.FraudFlags?.Count ?? 0,
            HasFraudCase = entity.FraudCase != null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
