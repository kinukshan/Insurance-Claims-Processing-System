using InsuranceClaims.Application.Authentication;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Authentication;

/// <summary>
/// Authentication service — handles registration, login, and user profile retrieval.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;

    public AuthService(
        ApplicationDbContext dbContext,
        PasswordService passwordService,
        JwtService jwtService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Register a new Policyholder account.
    /// Public registration is ALWAYS Policyholder — role cannot be escalated.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ArgumentException("First name is required.");
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ArgumentException("Last name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Password is required.");
        if (request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (request.Password != request.ConfirmPassword)
            throw new ArgumentException("Passwords do not match.");

        // Normalize email
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check for duplicate email
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser != null)
            throw new ArgumentException("An account with this email already exists.");

        // Create user — Role is ALWAYS Policyholder for public registration
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = _passwordService.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = Role.Policyholder,
            IsActive = true
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Generate JWT
        var token = _jwtService.GenerateToken(user);

        return new AuthResponse(
            Token: token,
            UserId: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Role: user.Role.ToString()
        );
    }

    /// <summary>
    /// Authenticate a user with email and password.
    /// Uses generic error messages to avoid email enumeration.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Generic error — do not reveal whether email exists
        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _jwtService.GenerateToken(user);

        return new AuthResponse(
            Token: token,
            UserId: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Role: user.Role.ToString()
        );
    }

    /// <summary>
    /// Get the current authenticated user's profile.
    /// </summary>
    public async Task<UserProfileResponse?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        return new UserProfileResponse(
            UserId: user.Id,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Role: user.Role.ToString(),
            IsActive: user.IsActive
        );
    }
}
