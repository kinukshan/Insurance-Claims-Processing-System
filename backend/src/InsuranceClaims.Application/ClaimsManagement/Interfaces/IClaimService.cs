using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.Application.ClaimsManagement.Interfaces;

/// <summary>
/// Service contract for claims management business operations.
/// </summary>
public interface IClaimService
{
    Task<ClaimResponseDto> CreateClaimAsync(Guid requestingUserId, CreateClaimDto dto, Role userRole = Role.Policyholder);
    Task<ClaimResponseDto?> GetClaimAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<List<ClaimSummaryDto>> GetMyClaimsAsync(Guid policyHolderId);
    Task<List<ClaimSummaryDto>> GetAllClaimsAsync(string? statusFilter = null, string? searchTerm = null, Guid? policyHolderId = null);
    Task<ClaimResponseDto?> UpdateClaimAsync(Guid claimId, Guid requestingUserId, UpdateClaimDto dto);
    Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId);
    Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<ClaimResponseDto?> SubmitClaimAsync(Guid claimId, Guid requestingUserId);
    Task<ClaimResponseDto?> WithdrawClaimAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<ClaimDocumentDto> AddDocumentAsync(Guid claimId, Guid requestingUserId, Role userRole, UploadDocumentDto dto);
    Task<bool> DeleteDocumentAsync(Guid claimId, Guid documentId, Guid requestingUserId, Role userRole);
    Task<List<ClaimDocumentDto>> GetDocumentsAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, Guid requestingUserId, Role userRole);
    Task<ClaimDocumentRequirementsDto?> GetDocumentRequirementsAsync(Guid claimId, Guid requestingUserId, Role userRole);
}
