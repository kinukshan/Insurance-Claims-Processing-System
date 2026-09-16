namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// Sandbox payment gateway for development and testing.
///
/// This is NOT a real payment processor. It simulates payment processing
/// with deterministic success/failure outcomes for testing.
///
/// Actual third-party payment integration will be completed separately
/// once the team selects a provider.
/// </summary>
public class SandboxPaymentGateway : IPaymentGateway
{
    /// <inheritdoc />
    public Task<PaymentResult> ProcessPaymentAsync(
        Guid payoutId, decimal amount, bool simulateFailure = false)
    {
        // Deterministic failure simulation for tests
        if (simulateFailure)
        {
            return Task.FromResult(new PaymentResult
            {
                Success = false,
                PaymentReference = null,
                ErrorMessage = "SANDBOX: Simulated payment failure for testing.",
                ProcessedAt = DateTime.UtcNow
            });
        }

        // Deterministic success
        var reference = $"SANDBOX-{payoutId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        return Task.FromResult(new PaymentResult
        {
            Success = true,
            PaymentReference = reference,
            ErrorMessage = null,
            ProcessedAt = DateTime.UtcNow
        });
    }
}
