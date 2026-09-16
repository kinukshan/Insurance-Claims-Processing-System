namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// Payment gateway abstraction.
/// 
/// This is a development-time abstraction only.
/// It does NOT satisfy the required third-party payment integration.
/// Actual third-party payment sandbox integration will be completed
/// by the team in a later phase.
/// 
/// No real financial transactions are performed.
/// No hard-coded API keys.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Process a payment in sandbox/test mode.
    /// </summary>
    /// <param name="payoutId">The payout identifier.</param>
    /// <param name="amount">The amount to process.</param>
    /// <param name="simulateFailure">When true, simulate a payment failure for testing.</param>
    /// <returns>Payment result with reference ID.</returns>
    Task<PaymentResult> ProcessPaymentAsync(Guid payoutId, decimal amount, bool simulateFailure = false);
}

/// <summary>
/// Result of a payment processing attempt.
/// </summary>
public class PaymentResult
{
    public bool Success { get; set; }
    public string? PaymentReference { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
