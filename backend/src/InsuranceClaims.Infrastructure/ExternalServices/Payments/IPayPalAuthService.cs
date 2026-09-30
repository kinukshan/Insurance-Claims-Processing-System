namespace InsuranceClaims.Infrastructure.ExternalServices.Payments;

/// <summary>
/// Server-side service for managing PayPal OAuth 2.0 access tokens.
/// Tokens are cached until near-expiry and refreshed thread-safely.
/// Client credentials and access tokens are NEVER logged or exposed.
/// </summary>
public interface IPayPalAuthService
{
    /// <summary>
    /// Get a valid Bearer access token for PayPal API calls.
    /// Returns the cached token if still valid, or requests a fresh token.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
