namespace InsuranceClaims.Application.Authentication;

/// <summary>
/// Registration request — public registration is Policyholder only.
/// </summary>
public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
);

/// <summary>
/// Login request.
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// Authentication response returned after successful login.
/// </summary>
public record AuthResponse(
    string Token,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role
);

/// <summary>
/// Current user profile response.
/// </summary>
public record UserProfileResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive
);
