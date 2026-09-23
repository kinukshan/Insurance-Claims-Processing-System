using System.Text.Json.Serialization;

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
    [JsonPropertyName("claim_id")]
    public Guid ClaimId { get; set; }

    [JsonPropertyName("policy_holder_id")]
    public Guid PolicyHolderId { get; set; }

    [JsonPropertyName("claim_amount")]
    public decimal ClaimAmount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("incident_date")]
    public DateTime IncidentDate { get; set; }

    [JsonPropertyName("incident_location")]
    public string IncidentLocation { get; set; } = string.Empty;
}

/// <summary>
/// Structured result from the AI fraud/risk agent.
/// </summary>
public class AiRiskResult
{
    [JsonPropertyName("risk_score")]
    public decimal RiskScore { get; set; }

    [JsonPropertyName("flags")]
    public List<AiRiskFlag> Flags { get; set; } = new();

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = "proceed";

    [JsonPropertyName("ai_used")]
    public bool AiUsed { get; set; }

    [JsonPropertyName("ai_provider")]
    public string? AiProvider { get; set; }

    [JsonPropertyName("ai_model")]
    public string? AiModel { get; set; }

    [JsonPropertyName("reasoning_summary")]
    public string? ReasoningSummary { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }
}

/// <summary>
/// A single flag from the AI agent's analysis.
/// </summary>
public class AiRiskFlag
{
    [JsonPropertyName("flag_type")]
    public string FlagType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Medium";
}
