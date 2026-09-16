using System.Net.Http.Json;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.AgentIntegration;

/// <summary>
/// Internal gateway from ASP.NET Core to the Validation / Safety Agent
/// running in the Python AI service.
///
/// Architecture: ASP.NET Core → Internal AI Service → Validation / Safety Agent
///
/// React and Flutter NEVER call the Python service directly.
/// This gateway uses HttpClient to call the internal AI service endpoint.
/// </summary>
public class PayoutValidationAgentGateway : IPayoutValidationAgentGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PayoutValidationAgentGateway> _logger;
    private readonly string _aiServiceBaseUrl;

    public PayoutValidationAgentGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PayoutValidationAgentGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aiServiceBaseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
    }

    /// <inheritdoc />
    public async Task<PayoutValidationResult> ValidatePayoutProposalAsync(
        PayoutValidationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_aiServiceBaseUrl}/api/validate/payout", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PayoutValidationResult>();
                return result ?? FallbackSafeResult();
            }

            _logger.LogWarning(
                "Validation Agent returned {StatusCode}. Falling back to safe default.",
                response.StatusCode);

            return FallbackSafeResult();
        }
        catch (Exception ex)
        {
            // Safe failure: if the agent is unreachable, require human approval
            _logger.LogError(ex,
                "Failed to reach Validation / Safety Agent. Applying safe fallback.");

            return FallbackSafeResult();
        }
    }

    /// <summary>
    /// Safe fallback when the agent is unreachable — require human approval.
    /// </summary>
    private static PayoutValidationResult FallbackSafeResult()
    {
        return new PayoutValidationResult
        {
            Valid = true,
            Violations = new List<string>(),
            RequiresHumanApproval = true,
            AgentId = "validation-safety-agent-fallback",
            Timestamp = DateTime.UtcNow
        };
    }
}
