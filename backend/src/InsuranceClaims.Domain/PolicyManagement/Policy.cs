using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.PolicyManagement.Enums;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents an insurance policy.
/// </summary>
public class Policy : BaseEntity
{
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid PolicyholderId { get; set; }
    public Guid PolicyTypeId { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal Premium { get; set; }
    public decimal Deductible { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public PolicyStatus Status { get; set; } = PolicyStatus.Draft;
    public RenewalStatus RenewalStatus { get; set; } = RenewalStatus.NotDue;
    public string? Exclusions { get; set; }

    // Navigation properties
    public PolicyType? PolicyType { get; set; }
    public ICollection<PolicyCoverage> Coverages { get; set; } = new List<PolicyCoverage>();

    // Business logic

    /// <summary>
    /// Returns true if the policy has passed its expiry date.
    /// </summary>
    public bool IsExpired() => DateTime.UtcNow > ExpiryDate;

    /// <summary>
    /// Returns true if the policy is eligible for renewal.
    /// A policy can be renewed if it is Active or Expired (within grace), and not already renewed/cancelled/lapsed.
    /// </summary>
    public bool CanRenew()
    {
        if (Status == PolicyStatus.Cancelled || Status == PolicyStatus.Lapsed)
            return false;

        if (RenewalStatus == RenewalStatus.Renewed)
            return false;

        return Status == PolicyStatus.Active || Status == PolicyStatus.Expired;
    }

    /// <summary>
    /// Marks the policy as lapsed.
    /// </summary>
    public void Lapse()
    {
        Status = PolicyStatus.Lapsed;
        RenewalStatus = RenewalStatus.Declined;
    }
}
