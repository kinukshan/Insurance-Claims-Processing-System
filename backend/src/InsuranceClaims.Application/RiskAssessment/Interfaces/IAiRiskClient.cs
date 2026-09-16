namespace InsuranceClaims.Application.RiskAssessment.Interfaces;

/// <summary>
/// Client interface for calling the internal AI fraud/risk assessment service.
/// The AI service is never called directly by React or Flutter.
/// </summary>
public interface IAiRiskClient
{
    /// <summary>
    /// Send claim data to the AI fraud/risk agent and receive a structured result.
    /// Returns null if the AI service is unavailable (safe fallback).
    /// </summary>
    Task<AiRiskResult?> AnalyzeClaimAsync(AiRiskRequest request);
}

/// <summary>
/// Structured request sent to the AI fraud/risk agent.
/// </summary>
public class AiRiskRequest
{
    public Guid ClaimId { get; set; }
    public Guid PolicyHolderId { get; set; }
    public decimal ClaimAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
    public string IncidentLocation { get; set; } = string.Empty;
}

/// <summary>
/// Structured result from the AI fraud/risk agent.
/// </summary>
public class AiRiskResult
{
    public decimal RiskScore { get; set; }
    public List<AiRiskFlag> Flags { get; set; } = new();
    public string Recommendation { get; set; } = "proceed";
}

/// <summary>
/// A single flag from the AI agent's analysis.
/// </summary>
public class AiRiskFlag
{
    public string FlagType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
}
