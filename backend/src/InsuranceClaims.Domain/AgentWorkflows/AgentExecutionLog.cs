using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.AgentWorkflows;

/// <summary>
/// Audit log for agent execution — structured output only.
/// </summary>
public class AgentExecutionLog : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public Guid? StepId { get; set; }
    public string LogLevel { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
