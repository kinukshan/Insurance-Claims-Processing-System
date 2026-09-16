namespace InsuranceClaims.Domain.ClaimsManagement;

/// <summary>
/// Verification status of a claim document, set by the Document Verification Agent.
/// </summary>
public enum DocumentVerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2,
    Flagged = 3
}
