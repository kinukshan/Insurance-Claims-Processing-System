using System.Net.Http.Json;
using System.Text.Json.Serialization;
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
                Complete: result?.Complete ?? false,
                MissingItems: result?.MissingItems ?? new List<string>(),
                Inconsistencies: result?.Inconsistencies?.Select(i => new DocumentInconsistencyDto(
                    i.Field ?? "unknown",
                    i.Description ?? "Unknown inconsistency",
                    i.Severity ?? "warning"
                )).ToList() ?? new List<DocumentInconsistencyDto>(),
                Warnings: result?.Warnings ?? new List<string>(),
                AiUsed: result?.AiUsed ?? false,
                AiProvider: result?.AiProvider,
                AiModel: result?.AiModel,
                ReasoningSummary: result?.ReasoningSummary,
                FallbackUsed: result?.FallbackUsed ?? false
            );
        }
        catch (Exception ex)
        {
            // Safe failure — return a structured error response instead of crashing
            return new DocumentVerificationResultDto(
                Complete: false,
                MissingItems: new List<string>(),
                Inconsistencies: new List<DocumentInconsistencyDto>(),
                Warnings: new List<string> { $"Document verification service unavailable: {ex.Message}" },
                AiUsed: false,
                AiProvider: null,
                AiModel: null,
                ReasoningSummary: null,
                FallbackUsed: true
            );
        }
    }

    // Internal deserialization models for the AI service response
    private record AiVerificationResponse
    {
        [JsonPropertyName("complete")]
        public bool Complete { get; init; }

        [JsonPropertyName("missing_items")]
        public List<string>? MissingItems { get; init; }

        [JsonPropertyName("inconsistencies")]
        public List<AiInconsistency>? Inconsistencies { get; init; }

        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; init; }

        [JsonPropertyName("ai_used")]
        public bool AiUsed { get; init; }

        [JsonPropertyName("ai_provider")]
        public string? AiProvider { get; init; }

        [JsonPropertyName("ai_model")]
        public string? AiModel { get; init; }

        [JsonPropertyName("reasoning_summary")]
        public string? ReasoningSummary { get; init; }

        [JsonPropertyName("fallback_used")]
        public bool FallbackUsed { get; init; }
    }

    private record AiInconsistency
    {
        [JsonPropertyName("field")]
        public string? Field { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("severity")]
        public string? Severity { get; init; }
    }
}
