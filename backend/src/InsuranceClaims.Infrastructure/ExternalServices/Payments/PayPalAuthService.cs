using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// Thread-safe PayPal OAuth 2.0 token service using client credentials flow.
/// Caches the Bearer token in memory until 2 minutes before expiry.
/// Never logs credentials or token contents.
/// </summary>
public class PayPalAuthService : IPayPalAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalAuthService> _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTime _tokenExpiryUtc = DateTime.MinValue;

    public PayPalAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PayPalAuthService>? logger = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger ?? NullLogger<PayPalAuthService>.Instance;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Return cached token if valid (with 2-minute safety window)
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryUtc)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring semaphore
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiryUtc)
            {
                return _cachedToken;
            }

            var mode = _configuration["PaymentGateway:PayPal:Mode"] ?? _configuration["PAYPAL_MODE"];
            if (string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Live mode is not supported. PayPal integration supports Sandbox only.");
            }

            var clientId = _configuration["PaymentGateway:PayPal:ClientId"]
                ?? _configuration["PAYPAL_CLIENT_ID"];
            var clientSecret = _configuration["PaymentGateway:PayPal:ClientSecret"]
                ?? _configuration["PAYPAL_CLIENT_SECRET"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogError("PayPal authentication failed: ClientId or ClientSecret is missing in configuration.");
                throw new InvalidOperationException("PayPal ClientId and ClientSecret must be configured.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                })
            };

            var basicAuthBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(basicAuthBytes));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _logger.LogInformation("Requesting PayPal OAuth access token from sandbox");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("PayPal token endpoint returned HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);

                throw new InvalidOperationException(
                    $"PayPal OAuth token request failed with status {(int)response.StatusCode}.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenProp) || string.IsNullOrEmpty(tokenProp.GetString()))
            {
                throw new InvalidOperationException("PayPal token response did not contain an access_token.");
            }

            _cachedToken = tokenProp.GetString()!;
            var expiresInSeconds = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;

            // Cache token until 2 minutes before expiry
            _tokenExpiryUtc = DateTime.UtcNow.AddSeconds(expiresInSeconds - 120);

            _logger.LogInformation("Successfully acquired PayPal access token, valid for {ExpiresIn} seconds", expiresInSeconds);
            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
