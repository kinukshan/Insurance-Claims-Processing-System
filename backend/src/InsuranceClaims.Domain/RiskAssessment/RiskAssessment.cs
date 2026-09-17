using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a risk assessment for a claim.
/// Links to Claim via ClaimId.
/// </summary>
public class RiskAssessment : BaseEntity
{
    /// <summary>FK to the assessed claim.</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Risk score between 0 (safe) and 100 (maximum risk).</summary>
    public decimal RiskScore { get; set; }

    /// <summary>Categorised risk level derived from the score.</summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>Recommendation produced by the assessment.</summary>
    public RiskRecommendation Recommendation { get; set; }

    /// <summary>Who or what produced this assessment.</summary>
    public AssessorType AssessorType { get; set; }

    /// <summary>UTC timestamp when the assessment was performed.</summary>
    public DateTime AssessmentTimestamp { get; set; }

    /// <summary>Auditable structured summary of the assessment result.</summary>
    public string Summary { get; set; } = string.Empty;

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>Fraud flags raised during this assessment.</summary>
    public ICollection<FraudFlag> FraudFlags { get; set; } = new List<FraudFlag>();

    /// <summary>Fraud case created from this assessment, if any.</summary>
    public FraudCase? FraudCase { get; set; }
}
