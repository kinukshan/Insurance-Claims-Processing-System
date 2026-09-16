using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a fraud case opened for investigation when a claim
/// is escalated due to high-risk indicators.
/// </summary>
public class FraudCase : BaseEntity
{
    /// <summary>FK to the associated claim.</summary>
    public Guid ClaimId { get; set; }

    /// <summary>FK to the risk assessment that triggered this case.</summary>
    public Guid RiskAssessmentId { get; set; }

    /// <summary>FK to the policyholder for history tracking.</summary>
    public Guid PolicyHolderId { get; set; }

    /// <summary>Current investigation status.</summary>
    public FraudCaseStatus Status { get; set; } = FraudCaseStatus.Open;

    /// <summary>Investigation priority.</summary>
    public FraudCasePriority Priority { get; set; } = FraudCasePriority.Medium;

    /// <summary>Staff member assigned to investigate.</summary>
    public string? AssignedReviewer { get; set; }

    /// <summary>Internal notes for the investigation (staff only).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Resolution description when the case is closed.</summary>
    public string? Resolution { get; set; }

    /// <summary>UTC timestamp when the case was closed.</summary>
    public DateTime? ClosedAt { get; set; }

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>Risk assessment that triggered this case.</summary>
    public RiskAssessment RiskAssessment { get; set; } = null!;
}
