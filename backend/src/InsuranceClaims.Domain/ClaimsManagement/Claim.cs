using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.PolicyManagement;

namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Represents an insurance claim submitted by a policyholder.
/// </summary>
public class Claim : BaseEntity
{
    public Guid PolicyId { get; set; }
    public Guid PolicyHolderId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public ClaimType ClaimType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal ClaimedAmount { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IncidentLocation { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public DateTime? SubmittedAt { get; set; }

    // Navigation properties
    public virtual Policy? Policy { get; set; }
    public virtual ICollection<ClaimDocument> Documents { get; set; } = new List<ClaimDocument>();
}

