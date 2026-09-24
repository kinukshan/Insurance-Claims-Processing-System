using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.Application.ClaimsManagement.DTOs;

/// <summary>
/// DTO for creating a new insurance claim.
/// </summary>
public record CreateClaimDto(
    Guid PolicyId,
    ClaimType ClaimType,
    DateTime IncidentDate,
    string IncidentLocation,
    string Description,
    decimal ClaimedAmount
);

/// <summary>
/// DTO for updating an existing draft claim.
/// </summary>
public record UpdateClaimDto(
    string? Description,
    string? IncidentLocation,
    decimal? ClaimedAmount,
    DateTime? IncidentDate
);

/// <summary>
/// Full claim response DTO for API responses.
/// </summary>
public record ClaimResponseDto(
    Guid Id,
    Guid PolicyId,
    Guid PolicyHolderId,
    string ClaimNumber,
    string ClaimType,
    string Description,
    decimal ClaimedAmount,
    DateTime IncidentDate,
    string IncidentLocation,
    string Status,
    DateTime? SubmittedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ClaimDocumentDto> Documents
);

/// <summary>
/// Lightweight claim DTO for list views.
/// </summary>
public record ClaimSummaryDto(
    Guid Id,
    string ClaimNumber,
    string ClaimType,
    decimal ClaimedAmount,
    string Status,
    DateTime IncidentDate,
    DateTime? SubmittedAt,
    DateTime CreatedAt
);

/// <summary>
/// DTO for claim document metadata.
/// </summary>
public record ClaimDocumentDto(
    Guid Id,
    Guid ClaimId,
    string FileName,
    string FileUrl,
    string DocumentType,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    string VerificationStatus
);

/// <summary>
/// DTO for uploading a document to a claim.
/// </summary>
public record UploadDocumentDto(
    string DocumentType,
    string FileName,
    string ContentType,
    long FileSize,
    Stream FileStream
);

/// <summary>
/// Result of policy coverage validation for a claim.
/// </summary>
public record CoverageValidationResultDto(
    bool IsValid,
    bool IsCovered,
    decimal? CoverageLimit,
    decimal? DeductibleAmount,
    string? CoverageType,
    List<string> Issues
);

/// <summary>
/// Result from the AI Document Verification Agent.
/// </summary>
public record DocumentVerificationResultDto(
    bool Complete,
    List<string> MissingItems,
    List<DocumentInconsistencyDto> Inconsistencies,
    List<string> Warnings,
    bool AiUsed = false,
    string? AiProvider = null,
    string? AiModel = null,
    string? ReasoningSummary = null,
    bool FallbackUsed = false
);

/// <summary>
/// Represents a single inconsistency found by the Document Verification Agent.
/// </summary>
public record DocumentInconsistencyDto(
    string Field,
    string Description,
    string Severity
);

/// <summary>
/// Deterministic document requirements response DTO for a claim.
/// </summary>
public record ClaimDocumentRequirementsDto(
    Guid ClaimId,
    string ClaimType,
    List<ClaimDocumentRequirementItemDto> RequiredDocuments,
    int RequiredCount,
    int UploadedRequiredCount,
    int MissingCount,
    bool Complete
);

/// <summary>
/// A single required document item and its upload status.
/// </summary>
public record ClaimDocumentRequirementItemDto(
    string Type,
    bool Required,
    bool Uploaded
);
