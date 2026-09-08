using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents a type/category of insurance policy.
/// </summary>
public class PolicyType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
