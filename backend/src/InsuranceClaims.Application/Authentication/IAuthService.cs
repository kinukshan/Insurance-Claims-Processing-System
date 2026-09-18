namespace InsuranceClaims.Application.Authentication;

/// <summary>
/// Authentication service interface.
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserProfileResponse?> GetCurrentUserAsync(Guid userId);
}
