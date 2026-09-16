using InsuranceClaims.Domain.RiskAssessment;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Response DTO for a fraud flag.
/// </summary>
public class FraudFlagDto
{
    public Guid Id { get; set; }
    public Guid RiskAssessmentId { get; set; }
    public Guid ClaimId { get; set; }
    public FraudFlagType FlagType { get; set; }
    public string FlagTypeDisplay => FlagType.ToString();
    public string Description { get; set; } = string.Empty;
    public FlagSeverity Severity { get; set; }
    public string SeverityDisplay => Severity.ToString();
    public FlagSource Source { get; set; }
    public string SourceDisplay => Source.ToString();
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Maps a domain FraudFlag entity to a DTO.
    /// </summary>
    public static FraudFlagDto FromEntity(FraudFlag entity)
    {
        return new FraudFlagDto
        {
            Id = entity.Id,
            RiskAssessmentId = entity.RiskAssessmentId,
            ClaimId = entity.ClaimId,
            FlagType = entity.FlagType,
            Description = entity.Description,
            Severity = entity.Severity,
            Source = entity.Source,
            IsResolved = entity.IsResolved,
            ResolvedAt = entity.ResolvedAt,
            ResolvedBy = entity.ResolvedBy,
            CreatedAt = entity.CreatedAt
        };
    }
}
