using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Represents an insurance claim submitted by a policyholder.
/// </summary>
public class Claim : BaseEntity
{
    public Guid PolicyId { get; set; }
    public Guid PolicyHolderId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ClaimAmount { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IncidentLocation { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    // TODO: Add navigation properties during implementation
}
