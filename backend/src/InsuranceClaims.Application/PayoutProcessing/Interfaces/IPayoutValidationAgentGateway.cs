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
/// </summary>
public class PayoutValidationRequest
{
    public Guid ClaimId { get; set; }
    public string PolicyType { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public decimal ApprovedClaimAmount { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal Deductible { get; set; }
    public decimal ProposedPayout { get; set; }
}

/// <summary>
/// Structured output contract from the Validation / Safety Agent.
/// </summary>
public class PayoutValidationResult
{
    public bool Valid { get; set; }
    public List<string> Violations { get; set; } = new();
    public bool RequiresHumanApproval { get; set; } = true;
    public string AgentId { get; set; } = "validation-safety-agent";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
