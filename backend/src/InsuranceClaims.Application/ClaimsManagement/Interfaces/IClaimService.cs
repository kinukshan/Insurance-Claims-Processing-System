using InsuranceClaims.Application.ClaimsManagement.DTOs;

namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Service contract for claims management business operations.
/// </summary>
public interface IClaimService
{
    Task<ClaimResponseDto> CreateClaimAsync(Guid policyHolderId, CreateClaimDto dto);
    Task<ClaimResponseDto?> GetClaimAsync(Guid claimId, Guid requestingUserId);
    Task<List<ClaimSummaryDto>> GetMyClaimsAsync(Guid policyHolderId);
    Task<List<ClaimSummaryDto>> GetAllClaimsAsync(string? statusFilter = null, string? searchTerm = null);
    Task<ClaimResponseDto?> UpdateClaimAsync(Guid claimId, Guid requestingUserId, UpdateClaimDto dto);
    Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId);
    Task<ClaimResponseDto?> SubmitClaimAsync(Guid claimId, Guid requestingUserId);
    Task<ClaimDocumentDto> AddDocumentAsync(Guid claimId, Guid requestingUserId, UploadDocumentDto dto);
    Task<List<ClaimDocumentDto>> GetDocumentsAsync(Guid claimId, Guid requestingUserId);
    Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid claimId, Guid requestingUserId);
    Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, Guid requestingUserId);
}
