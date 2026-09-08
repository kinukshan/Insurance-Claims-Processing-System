using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.AgentWorkflows;

/// <summary>
/// Represents an agentic AI workflow execution.
/// Stores structured workflow state only — no chain-of-thought or hidden reasoning.
/// </summary>
public class AgentWorkflow : BaseEntity
{
    public Guid ClaimId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string? FinalOutcome { get; set; }
    public string? ExecutionSummary { get; set; }

    // TODO: Add navigation properties during implementation
}
