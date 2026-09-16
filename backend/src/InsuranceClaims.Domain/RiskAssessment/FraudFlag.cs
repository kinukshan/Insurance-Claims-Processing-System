using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Domain.RiskAssessment;

/// <summary>
/// Represents a fraud flag raised during risk assessment.
/// Each flag captures one specific suspicious indicator.
/// </summary>
public class FraudFlag : BaseEntity
{
    /// <summary>FK to the parent risk assessment.</summary>
    public Guid RiskAssessmentId { get; set; }

    /// <summary>FK to the associated claim.</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Category of the flag.</summary>
    public FraudFlagType FlagType { get; set; }

    /// <summary>Human-readable description of why the flag was raised.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Severity of this flag.</summary>
    public FlagSeverity Severity { get; set; }

    /// <summary>Whether the flag was raised by a rule or by the AI agent.</summary>
    public FlagSource Source { get; set; }

    /// <summary>Whether this flag has been reviewed and resolved.</summary>
    public bool IsResolved { get; set; }

    /// <summary>UTC timestamp when the flag was resolved.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Identifier of the reviewer who resolved the flag.</summary>
    public string? ResolvedBy { get; set; }

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>Parent risk assessment.</summary>
    public RiskAssessment RiskAssessment { get; set; } = null!;
}
