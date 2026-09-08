using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.AgentWorkflows;

/// <summary>
/// Represents a single step in an agent workflow.
/// </summary>
public class AgentWorkflowStep : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    // TODO: Add properties during implementation
}
