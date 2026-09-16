using System.Net.Http.Json;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for calling the internal AI fraud/risk assessment service.
/// Implements safe fallback: if the AI service is unavailable, returns null
/// so the backend can proceed with deterministic rules only.
/// </summary>
public class AiRiskClient : IAiRiskClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiRiskClient> _logger;

    public AiRiskClient(HttpClient httpClient, ILogger<AiRiskClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiRiskResult?> AnalyzeClaimAsync(AiRiskRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/fraud-risk/assess", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI risk service returned {StatusCode} for claim {ClaimId}. Falling back to rules-only assessment.",
                    response.StatusCode, request.ClaimId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<AiRiskResult>();

            if (result == null)
            {
                _logger.LogWarning(
                    "AI risk service returned empty result for claim {ClaimId}. Falling back to rules-only assessment.",
                    request.ClaimId);
                return null;
            }

            // Validate the AI result
            result.RiskScore = Math.Clamp(result.RiskScore, 0m, 100m);

            if (result.Recommendation != "proceed" && result.Recommendation != "escalate")
            {
                _logger.LogWarning(
                    "AI risk service returned invalid recommendation '{Recommendation}' for claim {ClaimId}. Defaulting to 'escalate'.",
                    result.Recommendation, request.ClaimId);
                result.Recommendation = "escalate";
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "AI risk service is unreachable for claim {ClaimId}. Falling back to rules-only assessment.",
                request.ClaimId);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex,
                "AI risk service request timed out for claim {ClaimId}. Falling back to rules-only assessment.",
                request.ClaimId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error calling AI risk service for claim {ClaimId}. Falling back to rules-only assessment.",
                request.ClaimId);
            return null;
        }
    }
}
