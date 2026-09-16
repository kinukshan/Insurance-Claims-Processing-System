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
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DocumentVerificationStatus VerificationStatus { get; set; } = DocumentVerificationStatus.Pending;

    // Navigation property
    public virtual Claim? Claim { get; set; }
}

