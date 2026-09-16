using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Validators;
using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.Application.ClaimsManagement.Services;

/// <summary>
/// Implements claims management business logic including CRUD,
/// status transitions, document management, and AI verification.
/// </summary>
public class ClaimService : IClaimService
{
    private readonly IClaimRepository _claimRepository;
    private readonly IDocumentStorageService _storageService;
    private readonly IPolicyValidationService _policyValidation;
    private readonly IDocumentVerificationClient _verificationClient;

    public ClaimService(
        IClaimRepository claimRepository,
        IDocumentStorageService storageService,
        IPolicyValidationService policyValidation,
        IDocumentVerificationClient verificationClient)
    {
        _claimRepository = claimRepository;
        _storageService = storageService;
        _policyValidation = policyValidation;
        _verificationClient = verificationClient;
    }

    public async Task<ClaimResponseDto> CreateClaimAsync(Guid policyHolderId, CreateClaimDto dto)
    {
        // Validate input
        var errors = CreateClaimValidator.Validate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join("; ", errors));

        var claimNumber = await _claimRepository.GenerateClaimNumberAsync();

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            PolicyId = dto.PolicyId,
            PolicyHolderId = policyHolderId,
            ClaimNumber = claimNumber,
            ClaimType = dto.ClaimType,
            Description = dto.Description,
            ClaimedAmount = dto.ClaimedAmount,
            IncidentDate = dto.IncidentDate,
            IncidentLocation = dto.IncidentLocation,
            Status = ClaimStatus.Draft
        };

        var created = await _claimRepository.AddAsync(claim);
        return MapToResponse(created);
    }

    public async Task<ClaimResponseDto?> GetClaimAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return null;

        // Ownership check — a policyholder can only see their own claims
        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to view this claim.");

        return MapToResponse(claim);
    }

    public async Task<List<ClaimSummaryDto>> GetMyClaimsAsync(Guid policyHolderId)
    {
        var claims = await _claimRepository.GetByPolicyHolderIdAsync(policyHolderId);
        return claims.Select(MapToSummary).ToList();
    }

    public async Task<List<ClaimSummaryDto>> GetAllClaimsAsync(string? statusFilter = null, string? searchTerm = null)
    {
        var claims = await _claimRepository.GetAllAsync(statusFilter, searchTerm);
        return claims.Select(MapToSummary).ToList();
    }

    public async Task<ClaimResponseDto?> UpdateClaimAsync(Guid claimId, Guid requestingUserId, UpdateClaimDto dto)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return null;

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to update this claim.");

        // Only Draft claims can be updated
        if (claim.Status != ClaimStatus.Draft)
            throw new InvalidOperationException("Only draft claims can be updated.");

        var errors = UpdateClaimValidator.Validate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join("; ", errors));

        if (dto.Description is not null) claim.Description = dto.Description;
        if (dto.IncidentLocation is not null) claim.IncidentLocation = dto.IncidentLocation;
        if (dto.ClaimedAmount.HasValue) claim.ClaimedAmount = dto.ClaimedAmount.Value;
        if (dto.IncidentDate.HasValue) claim.IncidentDate = dto.IncidentDate.Value;

        var updated = await _claimRepository.UpdateAsync(claim);
        return MapToResponse(updated);
    }

    public async Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId);
        if (claim == null) return false;

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to delete this claim.");

        // Only Draft claims can be deleted (set to Withdrawn)
        if (claim.Status != ClaimStatus.Draft)
            throw new InvalidOperationException("Only draft claims can be withdrawn.");

        claim.Status = ClaimStatus.Withdrawn;
        await _claimRepository.UpdateAsync(claim);
        return true;
    }

    public async Task<ClaimResponseDto?> SubmitClaimAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return null;

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to submit this claim.");

        if (claim.Status != ClaimStatus.Draft)
            throw new InvalidOperationException("Only draft claims can be submitted.");

        claim.Status = ClaimStatus.Submitted;
        claim.SubmittedAt = DateTime.UtcNow;

        var updated = await _claimRepository.UpdateAsync(claim);
        return MapToResponse(updated);
    }

    public async Task<ClaimDocumentDto> AddDocumentAsync(Guid claimId, Guid requestingUserId, UploadDocumentDto dto)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to add documents to this claim.");

        // Cannot add documents to finalized claims
        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Rejected or ClaimStatus.Withdrawn or ClaimStatus.Closed)
            throw new InvalidOperationException("Cannot add documents to a finalized claim.");

        // Upload file to storage
        var fileUrl = await _storageService.UploadAsync(dto.FileName, dto.ContentType, dto.FileStream);

        var document = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            FileName = dto.FileName,
            FileUrl = fileUrl,
            DocumentType = dto.DocumentType,
            ContentType = dto.ContentType,
            FileSize = dto.FileSize,
            UploadedAt = DateTime.UtcNow,
            VerificationStatus = DocumentVerificationStatus.Pending
        };

        claim.Documents.Add(document);
        await _claimRepository.UpdateAsync(claim);

        return MapDocumentToDto(document);
    }

    public async Task<List<ClaimDocumentDto>> GetDocumentsAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to view documents for this claim.");

        return claim.Documents.Select(MapDocumentToDto).ToList();
    }

    public async Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to validate coverage for this claim.");

        return await _policyValidation.ValidateCoverageAsync(
            claim.PolicyId,
            claim.ClaimType.ToString(),
            claim.ClaimedAmount);
    }

    public async Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, Guid requestingUserId)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to verify documents for this claim.");

        var documentDtos = claim.Documents.Select(MapDocumentToDto).ToList();

        return await _verificationClient.VerifyDocumentsAsync(
            claimId,
            claim.ClaimType.ToString(),
            documentDtos,
            claim.IncidentDate,
            claim.ClaimedAmount);
    }

    // ───── Mapping helpers ─────

    private static ClaimResponseDto MapToResponse(Claim claim)
    {
        return new ClaimResponseDto(
            claim.Id,
            claim.PolicyId,
            claim.PolicyHolderId,
            claim.ClaimNumber,
            claim.ClaimType.ToString(),
            claim.Description,
            claim.ClaimedAmount,
            claim.IncidentDate,
            claim.IncidentLocation,
            claim.Status.ToString(),
            claim.SubmittedAt,
            claim.CreatedAt,
            claim.UpdatedAt,
            claim.Documents?.Select(MapDocumentToDto).ToList() ?? new List<ClaimDocumentDto>()
        );
    }

    private static ClaimSummaryDto MapToSummary(Claim claim)
    {
        return new ClaimSummaryDto(
            claim.Id,
            claim.ClaimNumber,
            claim.ClaimType.ToString(),
            claim.ClaimedAmount,
            claim.Status.ToString(),
            claim.IncidentDate,
            claim.SubmittedAt,
            claim.CreatedAt
        );
    }

    private static ClaimDocumentDto MapDocumentToDto(ClaimDocument doc)
    {
        return new ClaimDocumentDto(
            doc.Id,
            doc.ClaimId,
            doc.FileName,
            doc.FileUrl,
            doc.DocumentType,
            doc.ContentType,
            doc.FileSize,
            doc.UploadedAt,
            doc.VerificationStatus.ToString()
        );
    }
}
