namespace InsuranceClaims.Application.RiskAssessment.DTOs;

/// <summary>
/// Request DTO for triggering a risk assessment on a claim.
/// ClaimId is populated from the route parameter.
/// </summary>
public class AssessClaimRequest
{
    /// <summary>Whether to include AI agent analysis alongside deterministic rules.</summary>
    public bool IncludeAiAnalysis { get; set; } = true;

    /// <summary>Optional notes from the requesting staff member.</summary>
    public string? Notes { get; set; }
}
