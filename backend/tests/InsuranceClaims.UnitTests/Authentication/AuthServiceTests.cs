using InsuranceClaims.Application.Authentication;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Authentication;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InsuranceClaims.UnitTests.Authentication;

public class AuthServiceTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly JwtService _jwtService;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _passwordService = new PasswordService();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretTestKey_AtLeast32CharactersLong_ForHmacSha256!",
                ["Jwt:Issuer"] = "InsuranceClaims",
                ["Jwt:Audience"] = "InsuranceClaims.React",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        _jwtService = new JwtService(config);
        _authService = new AuthService(_dbContext, _passwordService, _jwtService);
    }

    // ── Registration Tests ───────────────────────────────────────────

    [Fact]
    public async Task Register_ValidPolicyholder_ReturnsAuthResponse()
    {
        var request = new RegisterRequest("John", "Doe", "john@test.com", "Password1!", "Password1!");
        var result = await _authService.RegisterAsync(request);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("john@test.com", result.Email);
        Assert.Equal("Policyholder", result.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsArgumentException()
    {
        var request = new RegisterRequest("John", "Doe", "duplicate@test.com", "Password1!", "Password1!");
        await _authService.RegisterAsync(request);

        var duplicate = new RegisterRequest("Jane", "Doe", "duplicate@test.com", "Password2!", "Password2!");
        await Assert.ThrowsAsync<ArgumentException>(() => _authService.RegisterAsync(duplicate));
    }

    [Fact]
    public async Task Register_AlwaysCreatesPolicyholder()
    {
        var request = new RegisterRequest("Admin", "User", "admin-attempt@test.com", "Password1!", "Password1!");
        var result = await _authService.RegisterAsync(request);

        Assert.Equal("Policyholder", result.Role);

        var user = await _dbContext.Users.FirstAsync(u => u.Email == "admin-attempt@test.com");
        Assert.Equal(Role.Policyholder, user.Role);
    }

    [Fact]
    public async Task Register_PasswordStoredAsHash()
    {
        var request = new RegisterRequest("Hash", "Test", "hash@test.com", "Password1!", "Password1!");
        await _authService.RegisterAsync(request);

        var user = await _dbContext.Users.FirstAsync(u => u.Email == "hash@test.com");
        Assert.NotEqual("Password1!", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt hash prefix
    }

    [Fact]
    public async Task Register_NormalizesEmail()
    {
        var request = new RegisterRequest("Email", "Test", "  TestUser@Example.COM  ", "Password1!", "Password1!");
        var result = await _authService.RegisterAsync(request);

        Assert.Equal("testuser@example.com", result.Email);
    }

    [Fact]
    public async Task Register_ShortPassword_ThrowsArgumentException()
    {
        var request = new RegisterRequest("Short", "Pass", "short@test.com", "12345", "12345");
        await Assert.ThrowsAsync<ArgumentException>(() => _authService.RegisterAsync(request));
    }

    [Fact]
    public async Task Register_MismatchedPasswords_ThrowsArgumentException()
    {
        var request = new RegisterRequest("Mismatch", "Pass", "mismatch@test.com", "Password1!", "DifferentPass!");
        await Assert.ThrowsAsync<ArgumentException>(() => _authService.RegisterAsync(request));
    }

    [Fact]
    public async Task Register_EmptyFirstName_ThrowsArgumentException()
    {
        var request = new RegisterRequest("", "Doe", "empty@test.com", "Password1!", "Password1!");
        await Assert.ThrowsAsync<ArgumentException>(() => _authService.RegisterAsync(request));
    }

    // ── Login Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange: register first
        await _authService.RegisterAsync(
            new RegisterRequest("Login", "User", "login@test.com", "Password1!", "Password1!"));

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("login@test.com", "Password1!"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("login@test.com", result.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsUnauthorized()
    {
        await _authService.RegisterAsync(
            new RegisterRequest("Wrong", "Pass", "wrongpass@test.com", "Password1!", "Password1!"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync(new LoginRequest("wrongpass@test.com", "WrongPassword!")));
    }

    [Fact]
    public async Task Login_NonexistentEmail_ThrowsGenericError()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync(new LoginRequest("nonexistent@test.com", "Password1!")));

        // Should use generic message — no email enumeration
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task Login_InactiveAccount_ThrowsUnauthorized()
    {
        await _authService.RegisterAsync(
            new RegisterRequest("Inactive", "User", "inactive@test.com", "Password1!", "Password1!"));

        // Deactivate
        var user = await _dbContext.Users.FirstAsync(u => u.Email == "inactive@test.com");
        user.IsActive = false;
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync(new LoginRequest("inactive@test.com", "Password1!")));
    }

    [Fact]
    public async Task Login_NormalizesEmail()
    {
        await _authService.RegisterAsync(
            new RegisterRequest("Normalize", "Login", "normalize@test.com", "Password1!", "Password1!"));

        var result = await _authService.LoginAsync(
            new LoginRequest("  NORMALIZE@Test.Com  ", "Password1!"));

        Assert.NotNull(result.Token);
    }

    // ── PasswordService Tests ────────────────────────────────────────

    [Fact]
    public void PasswordService_HashAndVerify_RoundTrip()
    {
        var password = "TestPassword123!";
        var hash = _passwordService.HashPassword(password);

        Assert.True(_passwordService.VerifyPassword(password, hash));
        Assert.False(_passwordService.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void PasswordService_DifferentHashes_ForSamePassword()
    {
        var password = "TestPassword123!";
        var hash1 = _passwordService.HashPassword(password);
        var hash2 = _passwordService.HashPassword(password);

        Assert.NotEqual(hash1, hash2); // BCrypt salts ensure different hashes
    }

    // ── GetCurrentUser Tests ─────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUser_ValidUser_ReturnsProfile()
    {
        var regResult = await _authService.RegisterAsync(
            new RegisterRequest("Profile", "User", "profile@test.com", "Password1!", "Password1!"));

        var profile = await _authService.GetCurrentUserAsync(regResult.UserId);

        Assert.NotNull(profile);
        Assert.Equal("profile@test.com", profile!.Email);
        Assert.Equal("Policyholder", profile.Role);
    }

    [Fact]
    public async Task GetCurrentUser_InvalidId_ReturnsNull()
    {
        var profile = await _authService.GetCurrentUserAsync(Guid.NewGuid());
        Assert.Null(profile);
    }
}
