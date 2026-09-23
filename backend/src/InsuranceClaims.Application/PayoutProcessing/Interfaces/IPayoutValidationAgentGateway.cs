using System.Text.Json.Serialization;

namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Internal gateway to the Validation / Safety Agent running in the Python AI service.
///
/// Architecture: ASP.NET Core → Internal AI Service → Validation / Safety Agent
///
/// React and Flutter NEVER call the Python service directly.
/// This interface is the narrowly-scoped contract for payout validation only.
/// Other agents (document verification, fraud/risk, coordinator) are NOT in scope here.
/// </summary>
public interface IPayoutValidationAgentGateway
{
    /// <summary>
    /// Request the Validation / Safety Agent to validate a payout proposal.
    /// </summary>
    Task<PayoutValidationResult> ValidatePayoutProposalAsync(PayoutValidationRequest request);
}

/// <summary>
/// Input contract for the Validation / Safety Agent.
/// Uses JsonPropertyName to map PascalCase to Python snake_case.
/// </summary>
public class PayoutValidationRequest
{
    [JsonPropertyName("claim_id")]
    public Guid ClaimId { get; set; }

    [JsonPropertyName("policy_type")]
    public string PolicyType { get; set; } = string.Empty;

    [JsonPropertyName("claim_type")]
    public string ClaimType { get; set; } = string.Empty;

    [JsonPropertyName("approved_claim_amount")]
    public decimal ApprovedClaimAmount { get; set; }

    [JsonPropertyName("coverage_limit")]
    public decimal CoverageLimit { get; set; }

    [JsonPropertyName("deductible")]
    public decimal Deductible { get; set; }

    [JsonPropertyName("proposed_payout")]
    public decimal ProposedPayout { get; set; }
}

/// <summary>
/// Structured output contract from the Validation / Safety Agent.
/// Uses JsonPropertyName for explicit mapping from Python snake_case responses.
/// </summary>
public class PayoutValidationResult
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("violations")]
    public List<string> Violations { get; set; } = new();

    [JsonPropertyName("requires_human_approval")]
    public bool RequiresHumanApproval { get; set; } = true;

    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = "validation-safety-agent";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

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
