using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Infrastructure.ExternalServices.Email;

/// <summary>
/// Mock email service for Phase 1 development and testing.
///
/// NO external network calls.
/// NO real emails sent.
/// NO SMTP/SendGrid/SES/Mailgun credentials required.
///
/// Generates deterministic, testable message IDs: MOCK-EMAIL-{guid-short}.
/// Supports configurable test outcomes via configuration:
///   Notification:Email:MockOutcome = "success" | "failure"
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly string _defaultOutcome;
    private readonly ILogger<MockEmailService> _logger;

    /// <summary>In-memory record of sent messages (for test assertions).</summary>
    private readonly List<EmailMessage> _sentMessages = new();
    private readonly object _lock = new();

    public MockEmailService(
        IConfiguration? configuration = null,
        ILogger<MockEmailService>? logger = null)
    {
        _defaultOutcome = configuration?["Notification:Email:MockOutcome"] ?? "success";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MockEmailService>.Instance;
    }

    /// <summary>
    /// Constructor for unit tests with explicit outcome control.
    /// </summary>
    public MockEmailService(string defaultOutcome)
    {
        _defaultOutcome = defaultOutcome;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MockEmailService>.Instance;
    }

    /// <inheritdoc />
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            _sentMessages.Add(message);
        }

        var messageId = $"MOCK-EMAIL-{Guid.NewGuid():N}"[..24];

        EmailSendResult result;

        if (string.Equals(_defaultOutcome, "failure", StringComparison.OrdinalIgnoreCase))
        {
            result = new EmailSendResult
            {
                Success = false,
                Provider = "Mock",
                MessageId = messageId,
                ErrorMessage = "MOCK: Simulated email delivery failure for testing."
            };

            _logger.LogInformation(
                "MockEmailService: Simulated FAILURE for {To} — Subject: {Subject}",
                message.To, message.Subject);
        }
        else
        {
            result = new EmailSendResult
            {
                Success = true,
                Provider = "Mock",
                MessageId = messageId,
                ErrorMessage = null
            };

            _logger.LogInformation(
                "MockEmailService: Simulated SUCCESS for {To} — Subject: {Subject} (MessageId: {MessageId})",
                message.To, message.Subject, messageId);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Returns all messages sent during this service instance's lifetime.
    /// Useful for test assertions.
    /// </summary>
    public IReadOnlyList<EmailMessage> GetSentMessages()
    {
        lock (_lock)
        {
            return _sentMessages.AsReadOnly();
        }
    }

    /// <summary>Clears the in-memory sent messages (for test isolation).</summary>
    public void ClearSentMessages()
    {
        lock (_lock)
        {
            _sentMessages.Clear();
        }
    }
}
