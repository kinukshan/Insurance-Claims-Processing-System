using InsuranceClaims.Domain.RiskAssessment.Enums;

namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Request DTO for escalating a risk assessment to a fraud case.
/// </summary>
public class EscalateRequest
{
    /// <summary>Reason for escalation.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Investigation priority.</summary>
    public FraudCasePriority Priority { get; set; } = FraudCasePriority.Medium;

    /// <summary>Staff member to assign the investigation to.</summary>
    public string? AssignedReviewer { get; set; }
}
