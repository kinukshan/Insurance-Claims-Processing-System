using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents a type/category of insurance policy (e.g., Auto, Home, Health, Life, Travel).
/// </summary>
public class PolicyType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Base premium rate used for premium calculation.
    /// </summary>
    public decimal BasePremiumRate { get; set; }

    /// <summary>
    /// Default coverage limit for this policy type.
    /// </summary>
    public decimal DefaultCoverageLimit { get; set; }

    /// <summary>
    /// Default deductible amount for this policy type.
    /// </summary>
    public decimal DefaultDeductible { get; set; }

    /// <summary>
    /// Risk multiplier applied during premium calculation.
    /// </summary>
    public decimal RiskMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// High-level regulatory and reporting insurance classification (General or LongTerm).
    /// </summary>
    public InsuranceClaims.Domain.PolicyManagement.Enums.InsuranceClass InsuranceClass { get; set; } = InsuranceClaims.Domain.PolicyManagement.Enums.InsuranceClass.General;

    /// <summary>
    /// Whether this policy type is currently offered.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Policy> Policies { get; set; } = new List<Policy>();
}
