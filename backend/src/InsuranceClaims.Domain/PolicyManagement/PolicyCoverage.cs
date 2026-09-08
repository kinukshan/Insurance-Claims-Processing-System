using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents coverage details within a policy.
/// </summary>
public class PolicyCoverage : BaseEntity
{
    public Guid PolicyId { get; set; }
    public string CoverageType { get; set; } = string.Empty;
    public decimal CoverageLimit { get; set; }
    public decimal DeductibleAmount { get; set; }

    // TODO: Add properties and navigation during implementation
}
