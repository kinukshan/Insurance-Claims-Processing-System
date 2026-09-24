using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AppGateway = InsuranceClaims.Application.PayoutProcessing.Interfaces;

namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// Mock payment gateway for Phase 1 development and testing.
///
/// NO external network calls.
/// NO real money movement.
/// NO paid service required.
///
/// Generates deterministic, testable transaction IDs: MOCK-PAY-{PayoutId short}.
/// Supports configurable test outcomes via configuration or constructor injection.
///
/// Default webhook validation token: "mock-webhook-secret-dev" (development only).
/// </summary>
public class MockPaymentGateway : AppGateway.IPaymentGateway
{
    /// <summary>
    /// Configurable default test outcome.
    /// Values: "success", "processing", "failure"
    /// </summary>
    private readonly string _defaultOutcome;

    /// <summary>Development-only webhook validation token.</summary>
    private readonly string _webhookToken;

    private readonly ILogger<MockPaymentGateway> _logger;

    /// <summary>In-memory ledger for idempotency within a single process lifetime.</summary>
    private readonly Dictionary<string, PaymentGatewayResult> _processedPayments = new();
    private readonly object _lock = new();

    public MockPaymentGateway(
        IConfiguration? configuration = null,
        ILogger<MockPaymentGateway>? logger = null)
    {
        _defaultOutcome = configuration?["PaymentGateway:MockOutcome"] ?? "success";
        _webhookToken = configuration?["PaymentGateway:MockWebhookToken"] ?? "mock-webhook-secret-dev";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MockPaymentGateway>.Instance;
    }

    /// <summary>
    /// Constructor for unit tests with explicit outcome control.
    /// </summary>
    public MockPaymentGateway(string defaultOutcome, string webhookToken = "mock-webhook-secret-dev")
    {
        _defaultOutcome = defaultOutcome;
        _webhookToken = webhookToken;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MockPaymentGateway>.Instance;
    }

    /// <inheritdoc />
    public Task<PaymentGatewayResult> CreatePayoutAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            // Idempotency: return existing result for same key
            if (_processedPayments.TryGetValue(request.IdempotencyKey, out var existing))
            {
                _logger.LogInformation(
                    "Mock payment: idempotent duplicate for key {IdempotencyKey}, returning existing result",
                    request.IdempotencyKey);
                return Task.FromResult(existing);
            }

            var transactionId = $"MOCK-PAY-{request.PayoutId:N}"[..24];
            var outcome = DetermineOutcome(request);

            var result = outcome switch
            {
                "failure" => new PaymentGatewayResult
                {
                    Success = false,
                    Provider = "Mock",
                    ProviderTransactionId = transactionId,
                    ProviderBatchId = transactionId,
                    ProviderItemId = transactionId,
                    ProviderStatus = "failed",
                    Message = "MOCK: Simulated payment failure for testing.",
                    FailureCode = "MOCK_FAILURE",
                    CreatedAt = DateTime.UtcNow
                },
                "processing" => new PaymentGatewayResult
                {
                    Success = true,
                    Provider = "Mock",
                    ProviderTransactionId = transactionId,
                    ProviderBatchId = transactionId,
                    ProviderItemId = transactionId,
                    ProviderStatus = "processing",
                    Message = "MOCK: Payment is being processed.",
                    CreatedAt = DateTime.UtcNow
                },
                _ => new PaymentGatewayResult
                {
                    Success = true,
                    Provider = "Mock",
                    ProviderTransactionId = transactionId,
                    ProviderBatchId = transactionId,
                    ProviderItemId = transactionId,
                    ProviderStatus = "succeeded",
                    Message = "MOCK: Payment completed successfully.",
                    CreatedAt = DateTime.UtcNow
                }
            };

            _processedPayments[request.IdempotencyKey] = result;

            _logger.LogInformation(
                "Mock payment created: PayoutId={PayoutId}, TransactionId={TransactionId}, Status={Status}",
                request.PayoutId, transactionId, result.ProviderStatus);

            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default)
    {
        // Mock: return a deterministic status based on the configured outcome
        var result = new PaymentGatewayStatusResult
        {
            ProviderTransactionId = providerTransactionId,
            Status = _defaultOutcome == "failure" ? "failed" : "succeeded",
            UpdatedAt = DateTime.UtcNow
        };

        return Task.FromResult<PaymentGatewayStatusResult?>(result);
    }

    /// <inheritdoc />
    public Task<bool> ValidateWebhookAsync(
        string payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        // Development-only validation: check for mock webhook token header
        if (headers.TryGetValue("X-Mock-Webhook-Token", out var token))
        {
            return Task.FromResult(
                string.Equals(token, _webhookToken, StringComparison.Ordinal));
        }

        _logger.LogWarning("Mock webhook validation failed: missing X-Mock-Webhook-Token header");
        return Task.FromResult(false);
    }

    /// <summary>
    /// Determine test outcome. Priority:
    /// 1. Description contains "SIMULATE_FAILURE" → failure
    /// 2. Description contains "SIMULATE_PROCESSING" → processing
    /// 3. Default configured outcome
    /// </summary>
    private string DetermineOutcome(PaymentGatewayRequest request)
    {
        if (request.Description.Contains("SIMULATE_FAILURE", StringComparison.OrdinalIgnoreCase))
            return "failure";
        if (request.Description.Contains("SIMULATE_PROCESSING", StringComparison.OrdinalIgnoreCase))
            return "processing";
        return _defaultOutcome;
    }
}
