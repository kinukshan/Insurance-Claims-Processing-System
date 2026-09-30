namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Application-level payment gateway abstraction.
///
/// PayoutService depends on this interface — never on a concrete provider.
/// Each provider (Mock, Stripe, PayPal, etc.) implements this interface
/// in the Infrastructure layer.
///
/// No vendor-specific HTTP code belongs in PayoutService.
/// No real money is moved in Phase 1 (MockPaymentGateway only).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Create a payout via the payment provider.
    /// The idempotency key in the request must be respected by the provider.
    /// </summary>
    Task<PaymentGatewayResult> CreatePayoutAsync(
        PaymentGatewayRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query current payment status from the provider.
    /// Returns null if the provider transaction ID is unknown.
    /// </summary>
    Task<PaymentGatewayStatusResult?> GetPaymentStatusAsync(
        string providerTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate an incoming webhook payload.
    /// Returns true if the payload is authentic for this provider.
    /// For Mock provider: simple development-only validation.
    /// For real providers: signature + timestamp verification.
    /// </summary>
    Task<bool> ValidateWebhookAsync(
        string payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provider-neutral payment request model.
/// Contains only data necessary for payment execution — no secrets,
/// no JWT tokens, no full policy records, no AI content.
/// </summary>
public class PaymentGatewayRequest
{
    /// <summary>Internal payout identifier.</summary>
    public Guid PayoutId { get; set; }

    /// <summary>Associated claim identifier.</summary>
    public Guid ClaimId { get; set; }

    /// <summary>Authoritative payout amount from approved payout record.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code. Default: USD.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Beneficiary reference (anonymized).</summary>
    public string BeneficiaryReference { get; set; } = string.Empty;

    /// <summary>Trusted recipient email address (from policyholder user profile).</summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Stable idempotency key generated from authoritative internal data.
    /// Same key must not create a second payment.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Stable sender batch identifier (e.g. insurance-payout-{PayoutId}).</summary>
    public string SenderBatchId { get; set; } = string.Empty;

    /// <summary>Stable sender item identifier (e.g. claim-{ClaimId}-payout-{PayoutId}).</summary>
    public string SenderItemId { get; set; } = string.Empty;

    /// <summary>Human-readable description for the payment.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Provider-neutral result from a payment creation attempt.
/// </summary>
public class PaymentGatewayResult
{
    /// <summary>Whether the payment was accepted by the provider.</summary>
    public bool Success { get; set; }

    /// <summary>Provider name (e.g., "Mock", "PayPalSandbox").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Provider's transaction identifier.</summary>
    public string? ProviderTransactionId { get; set; }

    /// <summary>Provider batch identifier (e.g. PayPal payout_batch_id).</summary>
    public string? ProviderBatchId { get; set; }

    /// <summary>Provider item identifier (e.g. PayPal payout_item_id).</summary>
    public string? ProviderItemId { get; set; }

    /// <summary>Provider-reported status (e.g., "processing", "succeeded", "failed").</summary>
    public string? ProviderStatus { get; set; }

    /// <summary>Human-readable message.</summary>
    public string? Message { get; set; }

    /// <summary>Provider failure code, if any.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Timestamp of result creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Provider-neutral payment status query result.
/// </summary>
public class PaymentGatewayStatusResult
{
    /// <summary>Provider's transaction identifier.</summary>
    public string ProviderTransactionId { get; set; } = string.Empty;

    /// <summary>Provider batch identifier, if applicable.</summary>
    public string? ProviderBatchId { get; set; }

    /// <summary>Provider item identifier, if applicable.</summary>
    public string? ProviderItemId { get; set; }

    /// <summary>Current normalized status string from provider (e.g. "processing", "succeeded", "failed").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Raw status string from provider (e.g. "PENDING", "SUCCESS").</summary>
    public string? RawStatus { get; set; }

    /// <summary>Whether query succeeded.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Human-readable status message.</summary>
    public string? Message { get; set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Failure code, if any.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Failure message, if any.</summary>
    public string? FailureMessage { get; set; }
}
