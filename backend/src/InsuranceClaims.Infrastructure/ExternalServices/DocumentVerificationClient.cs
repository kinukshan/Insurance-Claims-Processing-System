using System.Net.Http.Json;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;

namespace InsuranceClaims.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for communicating with the internal AI Document Verification Agent.
/// Architecture: ASP.NET Core -> AI Service (internal only).
/// </summary>
public class DocumentVerificationClient : IDocumentVerificationClient
{
    private readonly HttpClient _httpClient;

    public DocumentVerificationClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DocumentVerificationResultDto> VerifyDocumentsAsync(
        Guid claimId,
        string claimType,
        List<ClaimDocumentDto> documents,
        DateTime incidentDate,
        decimal claimedAmount)
    {
        var request = new
        {
            claim_id = claimId.ToString(),
            claim_type = claimType,
            incident_date = incidentDate.ToString("yyyy-MM-dd"),
            claimed_amount = claimedAmount,
            documents = documents.Select(d => new
            {
                document_type = d.DocumentType,
                file_name = d.FileName,
                uploaded_at = d.UploadedAt.ToString("yyyy-MM-dd"),
                verification_status = d.VerificationStatus
            }).ToList()
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agents/document-verification", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AiVerificationResponse>();

            return new DocumentVerificationResultDto(
                result?.Complete ?? false,
                result?.MissingItems ?? new List<string>(),
                result?.Inconsistencies?.Select(i => new DocumentInconsistencyDto(
                    i.Field ?? "unknown",
                    i.Description ?? "Unknown inconsistency",
                    i.Severity ?? "warning"
                )).ToList() ?? new List<DocumentInconsistencyDto>(),
                result?.Warnings ?? new List<string>()
            );
        }
        catch (Exception ex)
        {
            // Safe failure — return a structured error response instead of crashing
            return new DocumentVerificationResultDto(
                false,
                new List<string>(),
                new List<DocumentInconsistencyDto>(),
                new List<string> { $"Document verification service unavailable: {ex.Message}" }
            );
        }
    }

    // Internal deserialization models for the AI service response
    private record AiVerificationResponse
    {
        public bool Complete { get; init; }
        public List<string>? MissingItems { get; init; }
        public List<AiInconsistency>? Inconsistencies { get; init; }
        public List<string>? Warnings { get; init; }
    }

    private record AiInconsistency
    {
        public string? Field { get; init; }
        public string? Description { get; init; }
        public string? Severity { get; init; }
    }
}
