using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.PolicyManagement;

/// <summary>
/// Represents an insurance policy.
/// </summary>
public class Policy : BaseEntity
{
    public Guid PolicyHolderId { get; set; }
    public Guid PolicyTypeId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public bool IsActive { get; set; } = true;

    // TODO: Navigation properties and business logic to be added during implementation
}
