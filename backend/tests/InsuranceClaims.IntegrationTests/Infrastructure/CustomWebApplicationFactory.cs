using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InsuranceClaims.Domain.Common;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Persistence;
using InsuranceClaims.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace InsuranceClaims.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for spinning up an in-memory test server
/// with an isolated Entity Framework Core In-Memory database.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "SuperSecretTestKey_AtLeast32CharactersLong_ForHmacSha256!";
    public const string TestIssuer = "InsuranceClaims";
    public const string TestAudience = "InsuranceClaims.React";

    private static readonly SemaphoreSlim _seedLock = new(1, 1);
    private readonly string _databaseName = $"InsuranceClaims_Test_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["InMemoryDbName"] = _databaseName,
                ["Environment"] = "Testing",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=mock_db;",
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpiryMinutes"] = "60",
                ["AiService:BaseUrl"] = "http://localhost:8000"
            });
        });
    }

    /// <summary>
    /// Generates a valid signed JWT bearer token for the specified user and role.
    /// </summary>
    public string GenerateTestToken(Guid userId, string email, Role role, int expiryMinutes = 60)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Seeds canonical lookup data for testing into the in-memory database in a thread-safe manner.
    /// </summary>
    public async Task SeedReferenceDataAsync(Func<ApplicationDbContext, Task>? customSeed = null)
    {
        await _seedLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.EnsureCreatedAsync();

            if (!await db.PolicyTypes.AnyAsync())
            {
                await PolicyTypeSeeder.EnsurePolicyTypesSeededAsync(db);
            }

            if (customSeed != null)
            {
                await customSeed(db);
            }
        }
        finally
        {
            _seedLock.Release();
        }
    }
}
