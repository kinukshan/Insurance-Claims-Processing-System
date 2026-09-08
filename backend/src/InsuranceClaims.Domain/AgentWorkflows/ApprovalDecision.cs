using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.AgentWorkflows;

/// <summary>
/// Records a human approval decision on an agent workflow.
/// </summary>
public class ApprovalDecision : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public Guid ReviewerId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }

    // TODO: Add properties during implementation
}
