using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents coverage details within a policy.
/// </summary>
public class PolicyCoverage : BaseEntity
{
    public Guid PolicyId { get; set; }
    public string CoverageType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal DeductibleAmount { get; set; }

    /// <summary>
    /// Percentage of the total coverage this item represents (0-100).
    /// </summary>
    public decimal PercentageOfCoverage { get; set; }

    /// <summary>
    /// Whether this coverage is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Policy? Policy { get; set; }
}
