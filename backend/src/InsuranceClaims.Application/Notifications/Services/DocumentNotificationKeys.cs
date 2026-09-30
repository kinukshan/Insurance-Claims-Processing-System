using System.Security.Cryptography;
using System.Text;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Domain.ClaimsManagement;

namespace InsuranceClaims.Application.Notifications.Services;

/// <summary>
/// Generates deterministic, stable NotificationKey identifiers for document-related events.
/// Replaces weak count-based keys (e.g. "claim:{id}:docs-verified:{count}") with stable identifiers
/// derived from the actual workflow occurrence or document state fingerprint.
///
/// Guarantees:
/// - Same HTTP retry for same workflow event -> same NotificationKey -> one notification.
/// - Later new verification event with same document count -> different NotificationKey -> new legitimate notification allowed.
/// </summary>
public static class DocumentNotificationKeys
{
    /// <summary>
    /// Builds a stable key for AdditionalDocumentsRequired events.
    /// </summary>
    public static string ForAdditionalDocsRequired(
        Guid claimId,
        IEnumerable<string>? missingItems,
        IEnumerable<ClaimDocumentDto>? documents = null,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:additional-docs-required:{occurrenceId}";

        var missingSorted = missingItems != null
            ? string.Join(",", missingItems.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            : "none";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:additional-docs-required:{missingSorted}:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:additional-docs-required:{hash}";
    }

    /// <summary>
    /// Overload accepting domain entities.
    /// </summary>
    public static string ForAdditionalDocsRequired(
        Guid claimId,
        IEnumerable<string>? missingItems,
        IEnumerable<ClaimDocument>? documents,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:additional-docs-required:{occurrenceId}";

        var missingSorted = missingItems != null
            ? string.Join(",", missingItems.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            : "none";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:additional-docs-required:{missingSorted}:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:additional-docs-required:{hash}";
    }

    /// <summary>
    /// Builds a stable key for DocumentsNeedReview events.
    /// </summary>
    public static string ForDocumentsNeedReview(
        Guid claimId,
        IEnumerable<DocumentInconsistencyDto>? inconsistencies,
        IEnumerable<ClaimDocumentDto>? documents = null,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:documents-need-review:{occurrenceId}";

        var inconstSorted = inconsistencies != null
            ? string.Join(",", inconsistencies
                .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Description, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Field}:{x.Description}:{x.Severity}"))
            : "none";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:documents-need-review:{inconstSorted}:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:documents-need-review:{hash}";
    }

    /// <summary>
    /// Overload accepting domain entities.
    /// </summary>
    public static string ForDocumentsNeedReview(
        Guid claimId,
        IEnumerable<DocumentInconsistencyDto>? inconsistencies,
        IEnumerable<ClaimDocument>? documents,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:documents-need-review:{occurrenceId}";

        var inconstSorted = inconsistencies != null
            ? string.Join(",", inconsistencies
                .OrderBy(x => x.Field, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Description, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Field}:{x.Description}:{x.Severity}"))
            : "none";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:documents-need-review:{inconstSorted}:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:documents-need-review:{hash}";
    }

    /// <summary>
    /// Builds a stable key for DocumentsVerified events.
    /// </summary>
    public static string ForDocumentsVerified(
        Guid claimId,
        IEnumerable<ClaimDocumentDto>? documents = null,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:docs-verified:{occurrenceId}";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:docs-verified:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:docs-verified:{hash}";
    }

    /// <summary>
    /// Overload accepting domain entities.
    /// </summary>
    public static string ForDocumentsVerified(
        Guid claimId,
        IEnumerable<ClaimDocument>? documents,
        string? occurrenceId = null)
    {
        if (!string.IsNullOrWhiteSpace(occurrenceId))
            return $"claim:{claimId}:docs-verified:{occurrenceId}";

        var docsFingerprint = BuildDocsFingerprint(documents);
        var input = $"{claimId}:docs-verified:{docsFingerprint}";
        var hash = ComputeDeterministicHash(input);

        return $"claim:{claimId}:docs-verified:{hash}";
    }

    private static string BuildDocsFingerprint(IEnumerable<ClaimDocumentDto>? documents)
    {
        if (documents == null || !documents.Any())
            return "empty";

        return string.Join(";", documents
            .OrderBy(d => d.Id)
            .Select(d => $"{d.Id:N}:{d.DocumentType}:{d.VerificationStatus}:{d.UploadedAt:O}:{d.FileSize}"));
    }

    private static string BuildDocsFingerprint(IEnumerable<ClaimDocument>? documents)
    {
        if (documents == null || !documents.Any())
            return "empty";

        return string.Join(";", documents
            .OrderBy(d => d.Id)
            .Select(d => $"{d.Id:N}:{d.DocumentType}:{d.VerificationStatus}:{d.UploadedAt:O}:{d.FileSize}"));
    }

    private static string ComputeDeterministicHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
