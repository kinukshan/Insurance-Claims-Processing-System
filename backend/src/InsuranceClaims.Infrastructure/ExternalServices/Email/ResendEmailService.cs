using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.ExternalServices.Email;

/// <summary>
/// Resend email provider implementation using the REST API (POST /emails).
///
/// Design decisions:
///   - Uses typed HttpClient rather than the Resend .NET SDK for explicit control
///     over idempotency headers, request timeouts, and error handling.
///   - API key is read from backend-only configuration (User Secrets / env vars).
///     NEVER exposed to frontend, logs, or error messages.
///   - Supports a development recipient allowlist to prevent accidental delivery
///     to unintended addresses during testing.
///   - Resend's Idempotency-Key header (24h retention) is used alongside the
///     application's durable database reservation for defense-in-depth dedup.
///   - A successful Resend API response means the provider ACCEPTED the message.
///     It does NOT confirm final delivery to the recipient's mailbox.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _apiKey;
    private readonly string _senderAddress;
    private readonly string _senderName;
    private readonly HashSet<string> _recipientAllowlist;
    private readonly bool _allowlistEnabled;

    public ResendEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _apiKey = configuration["Notification:Email:Resend:ApiKey"]
            ?? configuration["RESEND_API_KEY"]
            ?? string.Empty;

        _senderAddress = configuration["Notification:Email:Resend:SenderAddress"]
            ?? configuration["RESEND_SENDER_ADDRESS"]
            ?? "onboarding@resend.dev";

        _senderName = configuration["Notification:Email:Resend:SenderName"]
            ?? configuration["RESEND_SENDER_NAME"]
            ?? "Insurance Claims";

        // Development recipient allowlist — when enabled, only listed addresses receive real emails
        var allowlistCsv = configuration["Notification:Email:Resend:RecipientAllowlist"]
            ?? configuration["RESEND_RECIPIENT_ALLOWLIST"]
            ?? string.Empty;

        _recipientAllowlist = allowlistCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .ToHashSet();

        _allowlistEnabled = _recipientAllowlist.Count > 0;
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // ── Fail-safe: missing API key ──────────────────────────────
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning(
                "Resend API key is not configured. Set Notification:Email:Resend:ApiKey via User Secrets or environment variable.");
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "Email provider is not configured. Please contact the system administrator."
            };
        }

        // ── Development recipient allowlist ─────────────────────────
        if (_allowlistEnabled && !_recipientAllowlist.Contains(message.To.ToLowerInvariant()))
        {
            _logger.LogInformation(
                "Resend: Recipient {Recipient} is not in the development allowlist — suppressing real email delivery",
                message.To);
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "Recipient is outside the development allowlist. Email was not sent."
            };
        }

        try
        {
            var from = string.IsNullOrWhiteSpace(_senderName)
                ? _senderAddress
                : $"{_senderName} <{_senderAddress}>";

            var payload = new ResendSendRequest
            {
                From = from,
                To = new[] { message.To },
                Subject = message.Subject,
                Html = message.Body
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            // Resend Idempotency-Key: use the application's stable notification key
            if (!string.IsNullOrWhiteSpace(message.IdempotencyKey))
            {
                // Resend allows up to 256 chars
                var idempotencyKey = message.IdempotencyKey.Length > 256
                    ? message.IdempotencyKey[..256]
                    : message.IdempotencyKey;
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            request.Content = JsonContent.Create(payload, options: _jsonOptions);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var resendResponse = JsonSerializer.Deserialize<ResendSendResponse>(responseBody, _jsonOptions);
                var emailId = resendResponse?.Id;

                _logger.LogInformation(
                    "Resend: Email accepted for {Recipient} — ResendId: {ResendId}",
                    message.To, emailId);

                return new EmailSendResult
                {
                    Success = true,
                    Provider = "Resend",
                    MessageId = emailId,
                    ErrorMessage = null
                };
            }

            // Parse error response for safe logging (never expose API key or full response to callers)
            var errorInfo = TryParseErrorMessage(responseBody);
            var safeError = $"Email provider returned HTTP {(int)response.StatusCode}. {errorInfo}";

            // Truncate for safe logging
            if (safeError.Length > 500)
                safeError = safeError[..500];

            _logger.LogWarning(
                "Resend: API error for {Recipient} — HTTP {StatusCode}: {Error}",
                message.To, (int)response.StatusCode, safeError);

            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = SanitizeErrorForPersistence(safeError)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Resend: Request was cancelled for {Recipient}", message.To);
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "Email send request was cancelled."
            };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout
            _logger.LogWarning(ex, "Resend: Request timed out for {Recipient}", message.To);
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "Email provider request timed out. The email may or may not have been accepted."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Resend: Network error for {Recipient}", message.To);
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "Unable to reach email provider. Please try again later."
            };
        }
        catch (Exception ex)
        {
            // Defensive catch-all — IEmailService contract says never throw on delivery failure
            _logger.LogError(ex, "Resend: Unexpected error for {Recipient}", message.To);
            return new EmailSendResult
            {
                Success = false,
                Provider = "Resend",
                MessageId = null,
                ErrorMessage = "An unexpected error occurred while sending the email."
            };
        }
    }

    /// <summary>
    /// Attempts to extract a human-readable error message from Resend's JSON error response.
    /// Never returns raw API keys or sensitive data.
    /// </summary>
    private static string TryParseErrorMessage(string responseBody)
    {
        try
        {
            var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
            {
                var msg = msgProp.GetString() ?? "Unknown error";
                // Strip any accidental inclusion of API key patterns
                return SanitizeErrorForPersistence(msg);
            }
        }
        catch
        {
            // Not valid JSON — return generic message
        }
        return "Provider returned an error.";
    }

    /// <summary>
    /// Ensures no API key patterns leak into persisted error messages.
    /// </summary>
    private static string SanitizeErrorForPersistence(string error)
    {
        if (string.IsNullOrEmpty(error))
            return error;

        // Remove anything that looks like an API key (re_XXXX pattern)
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            error, @"re_[A-Za-z0-9_\-]{6,}", "[REDACTED]");

        // Truncate to prevent excessively long error storage
        return sanitized.Length > 2000 ? sanitized[..2000] : sanitized;
    }

    // ── JSON Models ───────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class ResendSendRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string[] To { get; set; } = Array.Empty<string>();

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("html")]
        public string Html { get; set; } = string.Empty;
    }

    private sealed class ResendSendResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
