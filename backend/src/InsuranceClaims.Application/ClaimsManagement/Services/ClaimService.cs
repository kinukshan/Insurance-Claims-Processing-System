using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.ClaimsManagement.Validators;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;

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

    /// <summary>
    /// Returns true if the role is a staff role (ClaimsAdjuster, Underwriter, or Admin).
    /// Staff roles bypass claim ownership checks for read/inspect operations.
    /// </summary>
    private static bool IsStaffRole(Role role) =>
        role is Role.ClaimsAdjuster or Role.Underwriter or Role.Admin;

    public Task<ClaimResponseDto> CreateClaimAsync(Guid policyHolderId, CreateClaimDto dto) =>
        CreateClaimAsync(policyHolderId, dto, Role.Policyholder);

    public async Task<ClaimResponseDto> CreateClaimAsync(Guid requestingUserId, CreateClaimDto dto, Role userRole = Role.Policyholder)
    {
        // Validate input
        var errors = CreateClaimValidator.Validate(dto);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join("; ", errors));

        // 3. Load target policy WITH PolicyType
        var policy = await _policyValidation.GetPolicyDetailsAsync(dto.PolicyId);
        // 4. If policy does not exist -> 404
        if (policy == null)
            throw new KeyNotFoundException($"Policy with ID '{dto.PolicyId}' does not exist.");

        Guid policyHolderId;

        if (userRole == Role.Policyholder)
        {
            // 5. Verify policy belongs to authenticated Policyholder
            // 6. If not owner -> 403
            var ownsPolicy = await _policyValidation.ValidatePolicyOwnershipAsync(dto.PolicyId, requestingUserId);
            if (!ownsPolicy || (policy.PolicyholderId != Guid.Empty && policy.PolicyholderId != requestingUserId))
                throw new UnauthorizedAccessException("You do not have permission to submit a claim against this policy.");

            policyHolderId = requestingUserId;
        }
        else
        {
            // Staff claim creation:
            // 2. Authorize allowed staff role
            if (!IsStaffRole(userRole))
                throw new UnauthorizedAccessException("You do not have permission to submit a claim.");

            // 4. Derive owner from policy
            // 5. Set Claim.PolicyHolderId = policy.PolicyholderId (never staffUserId!)
            policyHolderId = policy.PolicyholderId;
        }

        // 7. ONLY THEN check policy/claim compatibility
        if (!PolicyClaimCompatibility.IsCompatible(policy.PolicyTypeName, dto.ClaimType))
        {
            // 8. If incompatible -> PolicyClaimCompatibilityException -> 400
            throw new PolicyClaimCompatibilityException(
                PolicyClaimCompatibility.GetErrorMessage(policy.PolicyTypeName, dto.ClaimType));
        }

        // 9. Create claim
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

    public async Task<ClaimResponseDto?> GetClaimAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return null;

        // Staff roles can view any claim; policyholders can only see their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to view this claim.");

        return MapToResponse(claim);
    }

    public async Task<List<ClaimSummaryDto>> GetMyClaimsAsync(Guid policyHolderId)
    {
        var claims = await _claimRepository.GetByPolicyHolderIdAsync(policyHolderId);
        return claims.Select(MapToSummary).ToList();
    }

    public async Task<List<ClaimSummaryDto>> GetAllClaimsAsync(string? statusFilter = null, string? searchTerm = null, Guid? policyHolderId = null)
    {
        var claims = await _claimRepository.GetAllAsync(statusFilter, searchTerm, policyHolderId);
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

    public Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId) =>
        DeleteClaimAsync(claimId, requestingUserId, Role.Policyholder);

    public async Task<bool> DeleteClaimAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        // Hard deletion of a Claim must be: Policyholder owner + Draft only.
        // Non-owner Policyholder, ClaimsAdjuster, Underwriter, Admin are all forbidden.
        if (userRole != Role.Policyholder)
            throw new UnauthorizedAccessException("Only the claimant policyholder can delete a draft claim.");

        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return false;

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to delete this claim.");

        if (claim.Status != ClaimStatus.Draft)
        {
            if (claim.Status is ClaimStatus.Submitted or ClaimStatus.UnderReview)
                throw new InvalidOperationException("Submitted or under review claims cannot be deleted. Use withdraw instead.");

            throw new InvalidOperationException("Cannot delete a finalized claim.");
        }

        // Clean up storage for any documents attached to this draft claim
        if (claim.Documents != null)
        {
            foreach (var doc in claim.Documents)
            {
                try
                {
                    await _storageService.DeleteAsync(doc.FileUrl);
                }
                catch
                {
                    // Best effort storage cleanup during cascade
                }
            }
        }

        await _claimRepository.DeleteAsync(claim);
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

    public async Task<ClaimResponseDto?> WithdrawClaimAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        // Withdraw represents the claimant voluntarily withdrawing the claim.
        // ClaimsAdjuster, Underwriter, Admin, and non-owner Policyholder are NOT allowed.
        if (userRole != Role.Policyholder)
            throw new UnauthorizedAccessException("Only the claimant policyholder can withdraw a claim.");

        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        if (claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to withdraw this claim.");

        // Allow only Submitted or UnderReview
        if (claim.Status != ClaimStatus.Submitted && claim.Status != ClaimStatus.UnderReview)
            throw new InvalidOperationException($"Cannot withdraw a claim with status '{claim.Status}'. Only submitted or under review claims can be withdrawn.");

        claim.Status = ClaimStatus.Withdrawn;
        claim.UpdatedAt = DateTime.UtcNow;

        var updated = await _claimRepository.UpdateAsync(claim);
        return MapToResponse(updated);
    }

    public async Task<ClaimDocumentDto> AddDocumentAsync(Guid claimId, Guid requestingUserId, Role userRole, UploadDocumentDto dto)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        // Staff roles can add documents to any claim; policyholders only their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
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

    public async Task<bool> DeleteDocumentAsync(Guid claimId, Guid documentId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        // Policyholder: own claim only; Staff: ClaimsAdjuster, Underwriter, Admin allowed
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to delete documents from this claim.");

        // Cannot delete documents from finalized claims
        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Rejected or ClaimStatus.Withdrawn or ClaimStatus.Closed)
            throw new InvalidOperationException("Cannot delete documents from a finalized claim.");

        var doc = claim.Documents?.FirstOrDefault(d => d.Id == documentId);
        if (doc == null)
            throw new KeyNotFoundException($"Document {documentId} not found on claim {claimId}.");

        // Delete physical file safely
        bool storageDeleted;
        try
        {
            storageDeleted = await _storageService.DeleteAsync(doc.FileUrl);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Storage deletion failed: {ex.Message}", ex);
        }

        if (!storageDeleted)
        {
            throw new InvalidOperationException("Storage deletion failed. Document was not removed.");
        }

        claim.Documents?.Remove(doc);
        await _claimRepository.DeleteDocumentAsync(doc);
        return true;
    }

    public async Task<List<ClaimDocumentDto>> GetDocumentsAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        // Staff roles can view documents for any claim; policyholders only their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to view documents for this claim.");

        return claim.Documents.Select(MapDocumentToDto).ToList();
    }

    public async Task<CoverageValidationResultDto> ValidateCoverageAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        // Staff roles can validate coverage for any claim; policyholders only their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to validate coverage for this claim.");

        return await _policyValidation.ValidateCoverageAsync(
            claim.PolicyId,
            claim.ClaimType.ToString(),
            claim.ClaimedAmount);
    }

    public async Task<DocumentVerificationResultDto> VerifyDocumentsAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null)
            throw new KeyNotFoundException($"Claim {claimId} not found.");

        // Staff roles can verify documents for any claim; policyholders only their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to verify documents for this claim.");

        var docs = claim.Documents ?? new List<ClaimDocument>();

        // 1. Fetch file bytes for each document safely for deterministic inspection
        var fileBytesMap = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            if (!string.IsNullOrWhiteSpace(doc.FileUrl))
            {
                try
                {
                    fileBytesMap[doc.FileUrl] = await _storageService.GetFileBytesAsync(doc.FileUrl);
                }
                catch
                {
                    fileBytesMap[doc.FileUrl] = null;
                }
            }
        }

        // 2. Perform deterministic document integrity validation
        var localEval = DocumentIntegrityValidator.EvaluateClaimDocuments(
            claim.ClaimType.ToString(),
            docs,
            url => fileBytesMap.TryGetValue(url, out var b) ? b : null
        );

        // 3. Update document verification statuses in database
        bool docUpdated = false;
        foreach (var doc in docs)
        {
            var docFinding = localEval.Findings.FirstOrDefault(f => f.DocumentId == doc.Id);
            if (docFinding != null)
            {
                if (doc.VerificationStatus != docFinding.Status)
                {
                    doc.VerificationStatus = docFinding.Status;
                    docUpdated = true;
                }
            }
            else if (doc.VerificationStatus != DocumentVerificationStatus.Rejected &&
                     doc.VerificationStatus != DocumentVerificationStatus.Verified)
            {
                doc.VerificationStatus = DocumentVerificationStatus.Verified;
                docUpdated = true;
            }
        }
        if (docUpdated)
        {
            await _claimRepository.UpdateAsync(claim);
        }

        var documentDtos = docs.Select(MapDocumentToDto).ToList();

        // 4. Call AI verification client (which also receives file metadata)
        DocumentVerificationResultDto aiResult;
        try
        {
            aiResult = await _verificationClient.VerifyDocumentsAsync(
                claimId,
                claim.ClaimType.ToString(),
                documentDtos,
                claim.IncidentDate,
                claim.ClaimedAmount);
        }
        catch
        {
            // Deterministic fallback if AI service is completely unavailable
            aiResult = new DocumentVerificationResultDto(
                Complete: !localEval.HasMismatches && !localEval.HasUnreadable,
                MissingItems: new List<string>(),
                Inconsistencies: new List<DocumentInconsistencyDto>(),
                Warnings: new List<string> { "AI verification service unavailable; deterministic rule-based validation applied." },
                AiUsed: false,
                AiProvider: null,
                AiModel: null,
                ReasoningSummary: null,
                FallbackUsed: true
            );
        }

        // 5. Merge deterministic findings into result (Authoritative deterministic rules)
        var mergedInconsistencies = new List<DocumentInconsistencyDto>(aiResult.Inconsistencies ?? new List<DocumentInconsistencyDto>());
        foreach (var finding in localEval.Findings)
        {
            if (!mergedInconsistencies.Any(i => i.Field.Equals(finding.DocumentType, StringComparison.OrdinalIgnoreCase) && i.Description.Equals(finding.Description, StringComparison.OrdinalIgnoreCase)))
            {
                mergedInconsistencies.Add(new DocumentInconsistencyDto(
                    finding.DocumentType,
                    finding.Description,
                    finding.Severity.ToString().ToLowerInvariant()
                ));
            }
        }

        var isComplete = aiResult.Complete && !localEval.HasMismatches && !localEval.HasUnreadable;

        return aiResult with
        {
            Complete = isComplete,
            Inconsistencies = mergedInconsistencies
        };
    }

    public async Task<ClaimDocumentRequirementsDto?> GetDocumentRequirementsAsync(Guid claimId, Guid requestingUserId, Role userRole)
    {
        var claim = await _claimRepository.GetByIdWithDocumentsAsync(claimId);
        if (claim == null) return null;

        // Staff roles can view any claim; policyholders can only see their own
        if (!IsStaffRole(userRole) && claim.PolicyHolderId != requestingUserId)
            throw new UnauthorizedAccessException("You do not have permission to view this claim.");

        var claimTypeStr = claim.ClaimType.ToString();
        var requiredList = DocumentChecklistValidator.GetRequiredDocuments(claimTypeStr);

        var submittedNormalized = new HashSet<string>(
            (claim.Documents ?? new List<ClaimDocument>())
                .Select(d => DocumentChecklistValidator.NormalizeDocumentType(d.DocumentType)),
            StringComparer.OrdinalIgnoreCase);

        var items = requiredList.Select(req =>
        {
            var normalizedReq = DocumentChecklistValidator.NormalizeDocumentType(req);
            var isUploaded = submittedNormalized.Contains(normalizedReq);
            return new ClaimDocumentRequirementItemDto(
                req,
                true,
                isUploaded
            );
        }).ToList();

        var uploadedCount = items.Count(i => i.Uploaded);
        var missingCount = items.Count - uploadedCount;

        return new ClaimDocumentRequirementsDto(
            claim.Id,
            claimTypeStr,
            items,
            items.Count,
            uploadedCount,
            missingCount,
            missingCount == 0
        );
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
