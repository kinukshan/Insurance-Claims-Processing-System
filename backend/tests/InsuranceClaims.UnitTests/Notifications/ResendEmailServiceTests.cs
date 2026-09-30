using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.Notifications.Services;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Infrastructure;
using InsuranceClaims.Infrastructure.ExternalServices.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsuranceClaims.UnitTests.Notifications;

/// <summary>
/// Comprehensive regression tests for Resend email integration.
/// Covers:
/// - Configuration and provider selection (Mock vs Resend)
/// - Safe failure on missing credentials
/// - Development recipient allowlist filtering
/// - Request formatting (headers, body, auth, idempotency)
/// - Success response handling and ID capture
/// - Provider failure response handling and credential redaction
/// - Cancellation, timeout, and network exception resilience
/// - NotificationOrchestrator acceptance distinction (Accepted vs Sent)
/// </summary>
public class ResendEmailServiceTests
{
    // ───── Helper: Mock HTTP Handler ──────────────────────────────────────────

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return await _handler(request, cancellationToken);
        }
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    // ───── 1. Configuration & Provider Selection Tests ─────────────────────────

    [Fact]
    public void DependencyInjection_DefaultConfiguration_ResolvesMockEmailService()
    {
        // Arrange — default config has no explicit Notification:Email:Provider
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
            ["Jwt:Key"] = "SuperSecretKeyThatIsLongEnoughForHmacSha256Security12345!"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetService<IEmailService>();

        // Assert
        Assert.NotNull(emailService);
        Assert.IsType<MockEmailService>(emailService);
    }

    [Fact]
    public void DependencyInjection_ExplicitMockProvider_ResolvesMockEmailService()
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
            ["Jwt:Key"] = "SuperSecretKeyThatIsLongEnoughForHmacSha256Security12345!",
            ["Notification:Email:Provider"] = "Mock"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetService<IEmailService>();

        // Assert
        Assert.NotNull(emailService);
        Assert.IsType<MockEmailService>(emailService);
    }

    [Fact]
    public void DependencyInjection_ExplicitResendProvider_ResolvesResendEmailService()
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
            ["Jwt:Key"] = "SuperSecretKeyThatIsLongEnoughForHmacSha256Security12345!",
            ["Notification:Email:Provider"] = "Resend",
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        var emailService = provider.GetService<IEmailService>();

        // Assert
        Assert.NotNull(emailService);
        Assert.IsType<ResendEmailService>(emailService);
    }

    [Fact]
    public void DependencyInjection_UnsupportedProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
            ["Jwt:Key"] = "SuperSecretKeyThatIsLongEnoughForHmacSha256Security12345!",
            ["Notification:Email:Provider"] = "SendGrid"
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(config));
        Assert.Contains("Unsupported email provider 'SendGrid'", ex.Message);
    }

    // ───── 2. Missing Credentials Fail-Safe Tests ─────────────────────────────

    [Fact]
    public async Task SendAsync_MissingApiKey_FailsSafelyWithoutSending()
    {
        // Arrange — empty ApiKey
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = ""
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "user@example.com",
            Subject = "Test",
            Body = "<p>Hello</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Null(result.MessageId);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Equal(0, handler.CallCount); // Never called HTTP
    }

    // ───── 3. Development Recipient Allowlist Tests ───────────────────────────

    [Fact]
    public async Task SendAsync_RecipientNotInAllowlist_SuppressedSafely()
    {
        // Arrange — allowlist is configured with only specific email
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123",
            ["Notification:Email:Resend:RecipientAllowlist"] = "developer@example.com, tester@example.com"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "unauthorized-policyholder@example.com",
            Subject = "Claim Notification",
            Body = "<p>Your claim has been processed.</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Null(result.MessageId);
        Assert.Contains("outside the development allowlist", result.ErrorMessage);
        Assert.Equal(0, handler.CallCount); // Suppressed — no HTTP call
    }

    [Fact]
    public async Task SendAsync_RecipientInAllowlist_SendsSuccessfully()
    {
        // Arrange — recipient is in allowlist (case-insensitive)
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123",
            ["Notification:Email:Resend:RecipientAllowlist"] = "Developer@Example.com"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "resend_msg_001" })
            };
            return Task.FromResult(response);
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "developer@example.com",
            Subject = "Test Allowlist",
            Body = "<p>Allowed</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Equal("resend_msg_001", result.MessageId);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_EmptyAllowlist_AllowsAllRecipients()
    {
        // Arrange — allowlist is empty (disabled)
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123",
            ["Notification:Email:Resend:RecipientAllowlist"] = ""
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "resend_msg_002" })
            };
            return Task.FromResult(response);
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "anyone@example.com",
            Subject = "Public Test",
            Body = "<p>Open</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, handler.CallCount);
    }

    // ───── 4. Resend Request Formatting & Headers ─────────────────────────────

    [Fact]
    public async Task SendAsync_CorrectHeadersPayloadAndIdempotencyKey()
    {
        // Arrange
        var apiKey = "re_secret_api_key_456";
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = apiKey,
            ["Notification:Email:Resend:SenderAddress"] = "claims@myinsurance.com",
            ["Notification:Email:Resend:SenderName"] = "Apex Insurance"
        });

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = req;
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "resend_id_12345" })
            };
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Claim Approved: CLM-101",
            Body = "<p>Your claim is approved.</p>",
            IdempotencyKey = "claim:abc-123:approved"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert — SendResult
        Assert.True(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Equal("resend_id_12345", result.MessageId);
        Assert.Null(result.ErrorMessage);

        // Assert — Request headers
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.resend.com/emails", capturedRequest.RequestUri?.ToString());
        Assert.Equal($"Bearer {apiKey}", capturedRequest.Headers.Authorization?.ToString());
        Assert.True(capturedRequest.Headers.Contains("Idempotency-Key"));
        Assert.Equal("claim:abc-123:approved", capturedRequest.Headers.GetValues("Idempotency-Key").First());

        // Assert — Request JSON body
        Assert.NotNull(capturedBody);
        using var jsonDoc = JsonDocument.Parse(capturedBody);
        var root = jsonDoc.RootElement;
        Assert.Equal("Apex Insurance <claims@myinsurance.com>", root.GetProperty("from").GetString());
        Assert.Equal("client@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Claim Approved: CLM-101", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Your claim is approved.</p>", root.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendAsync_LongIdempotencyKey_TruncatedTo256Chars()
    {
        // Arrange — IdempotencyKey longer than 256 chars
        var longKey = new string('x', 300);
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123"
        });

        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "resend_id_trunc" })
            });
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test",
            Body = "<p>Test</p>",
            IdempotencyKey = longKey
        };

        // Act
        await service.SendAsync(msg);

        // Assert
        Assert.NotNull(capturedRequest);
        var sentKey = capturedRequest.Headers.GetValues("Idempotency-Key").First();
        Assert.Equal(256, sentKey.Length);
        Assert.Equal(longKey[..256], sentKey);
    }

    // ───── 5. Provider Error & Redaction Tests ────────────────────────────────

    [Fact]
    public async Task SendAsync_ApiErrorResponse_ParsesErrorAndRedactsApiKeyPatterns()
    {
        // Arrange — Resend returns 422 with a message that simulates an API key echo
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_secret_key_123"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { message = "Invalid domain for key re_test_secret_key_123. Verify domain." }))
            };
            return Task.FromResult(response);
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test Error",
            Body = "<p>Test</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Null(result.MessageId);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("422", result.ErrorMessage);
        Assert.Contains("Invalid domain", result.ErrorMessage);
        // CRITICAL: ApiKey pattern must be redacted!
        Assert.DoesNotContain("re_test_secret_key_123", result.ErrorMessage);
        Assert.Contains("[REDACTED]", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_NonJsonErrorResponse_ReturnsSafeGenericError()
    {
        // Arrange — Resend returns 502 Bad Gateway with raw HTML / gateway message
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_secret_key_123"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("<html><body>502 Bad Gateway</body></html>")
            };
            return Task.FromResult(response);
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test",
            Body = "<p>Test</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Contains("502", result.ErrorMessage);
        Assert.Contains("Provider returned an error", result.ErrorMessage);
    }

    // ───── 6. Cancellation, Timeout, and Network Exceptions ───────────────────

    [Fact]
    public async Task SendAsync_CancellationRequested_ReturnsCancelledResult()
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            throw new OperationCanceledException(ct);
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test Cancel",
            Body = "<p>Test</p>"
        };

        // Act
        var result = await service.SendAsync(msg, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_TimeoutException_ReturnsSafeTimeoutMessage()
    {
        // Arrange — HttpClient timeout (TaskCanceledException when caller token is not cancelled)
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            throw new TaskCanceledException("The operation was canceled due to timeout.");
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test Timeout",
            Body = "<p>Test</p>"
        };

        // Act
        var result = await service.SendAsync(msg, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Must explain ambiguity:
        Assert.Contains("may or may not have been accepted", result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_HttpRequestException_ReturnsSafeNetworkErrorMessage()
    {
        // Arrange
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Notification:Email:Resend:ApiKey"] = "re_test_key_123"
        });

        var handler = new MockHttpMessageHandler((req, ct) =>
        {
            throw new HttpRequestException("Connection refused (api.resend.com:443)");
        });
        var client = new HttpClient(handler);
        var service = new ResendEmailService(client, config, NullLogger<ResendEmailService>.Instance);

        var msg = new EmailMessage
        {
            To = "client@example.com",
            Subject = "Test Network Error",
            Body = "<p>Test</p>"
        };

        // Act
        var result = await service.SendAsync(msg);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Resend", result.Provider);
        Assert.Contains("Unable to reach email provider", result.ErrorMessage);
        Assert.DoesNotContain("Connection refused", result.ErrorMessage); // Safe user-facing message
    }

    // ───── 7. NotificationOrchestrator Delivery vs Acceptance Distinction ─────

    private class FakeResendEmailService : IEmailService
    {
        private readonly bool _success;
        private readonly string? _messageId;
        private readonly string? _errorMessage;

        public FakeResendEmailService(bool success, string? messageId = "res_accepted_123", string? errorMessage = null)
        {
            _success = success;
            _messageId = messageId;
            _errorMessage = errorMessage;
        }

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResult
            {
                Success = _success,
                Provider = "Resend",
                MessageId = _messageId,
                ErrorMessage = _errorMessage
            });
        }
    }

    [Fact]
    public async Task Orchestrator_WithResendProvider_MarksStatusAsAcceptedNotSent()
    {
        // Arrange — Resend provider succeeds -> Orchestrator MUST use MarkAcceptedAsync
        var emailService = new FakeResendEmailService(success: true, messageId: "resend_email_999");
        var logRepo = new EmailNotificationTests.InMemoryNotificationLogRepository();
        var orchestrator = new NotificationOrchestrator(
            emailService, logRepo, NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        // Act
        var result = await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimApproved, "CLM-777");

        // Assert
        Assert.True(result);
        Assert.Single(logRepo.Logs);
        var log = logRepo.Logs[0];
        Assert.Equal("Resend", log.Provider);
        Assert.Equal("resend_email_999", log.ProviderMessageId);
        Assert.True(log.Success);
        // CRITICAL: Status must be Accepted (4), NOT Sent (2)
        Assert.Equal(NotificationStatus.Accepted, log.Status);
    }

    [Fact]
    public async Task Orchestrator_WithMockProvider_MarksStatusAsSent()
    {
        // Arrange — Mock provider succeeds -> Orchestrator uses MarkSentAsync
        var emailService = new MockEmailService("success");
        var logRepo = new EmailNotificationTests.InMemoryNotificationLogRepository();
        var orchestrator = new NotificationOrchestrator(
            emailService, logRepo, NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();

        // Act
        var result = await orchestrator.NotifyAsync(
            userId, "user@example.com", claimId,
            NotificationType.ClaimApproved, "CLM-888");

        // Assert
        Assert.True(result);
        Assert.Single(logRepo.Logs);
        var log = logRepo.Logs[0];
        Assert.Equal("Mock", log.Provider);
        Assert.True(log.Success);
        Assert.Equal(NotificationStatus.Sent, log.Status);
    }

    [Fact]
    public async Task Orchestrator_DuplicateAfterResendAccepted_IsSkippedIdempotently()
    {
        // Arrange
        var emailService = new FakeResendEmailService(success: true, messageId: "resend_email_abc");
        var logRepo = new EmailNotificationTests.InMemoryNotificationLogRepository();
        var orchestrator = new NotificationOrchestrator(
            emailService, logRepo, NullLogger<NotificationOrchestrator>.Instance);

        var userId = Guid.NewGuid();
        var claimId = Guid.NewGuid();
        var key = $"claim:{claimId}:payout";

        // Act 1 — First send
        var first = await orchestrator.NotifyAsync(
            key, userId, "user@example.com", claimId,
            NotificationType.PayoutCompleted, "CLM-999");

        // Act 2 — Duplicate send with the same key
        var second = await orchestrator.NotifyAsync(
            key, userId, "user@example.com", claimId,
            NotificationType.PayoutCompleted, "CLM-999");

        // Assert
        Assert.True(first);
        Assert.True(second); // Returns true (already processed)
        Assert.Single(logRepo.Logs); // Exactly 1 log entry in repository
        Assert.Equal(NotificationStatus.Accepted, logRepo.Logs[0].Status);
    }
}
