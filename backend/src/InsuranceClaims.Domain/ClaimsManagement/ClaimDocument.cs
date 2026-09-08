using InsuranceClaims.Domain.Common;

namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Represents a document attached to a claim (photo, receipt, report, etc.).
/// </summary>
public class ClaimDocument : BaseEntity
{
    public Guid ClaimId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;

    // TODO: Add properties during implementation
}
