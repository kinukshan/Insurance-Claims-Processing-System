using System.Security.Claims;
using InsuranceClaims.Domain.Users;
using InsuranceClaims.Infrastructure.Authentication;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Development-only seed endpoint for creating staff test accounts.
/// Guarded by environment check — will not function outside Development.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public SeedController(
        ApplicationDbContext dbContext,
        PasswordService passwordService,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _env = env;
        _configuration = configuration;
    }

    /// <summary>
    /// POST /api/seed/staff — Create test staff accounts.
    /// Development environment only. Password from User Secrets.
    /// </summary>
    [HttpPost("staff")]
    public async Task<IActionResult> SeedStaffAccounts()
    {
        if (!_env.IsDevelopment())
            return NotFound(); // Silently return 404 outside development

        var staffPassword = _configuration["Seed:StaffPassword"];
        if (string.IsNullOrEmpty(staffPassword))
            return BadRequest(new { error = "Seed:StaffPassword not configured in User Secrets." });

        var staffAccounts = new[]
        {
            new { Email = "adjuster@test.com", FirstName = "Sarah", LastName = "Johnson", Role = Role.ClaimsAdjuster },
            new { Email = "underwriter@test.com", FirstName = "Michael", LastName = "Chen", Role = Role.Underwriter },
            new { Email = "admin@test.com", FirstName = "Emily", LastName = "Williams", Role = Role.Admin },
        };

        var created = new List<string>();

        foreach (var account in staffAccounts)
        {
            var exists = await _dbContext.Users.AnyAsync(u => u.Email == account.Email);
            if (exists)
            {
                created.Add($"{account.Email} (already exists)");
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = account.Email,
                PasswordHash = _passwordService.HashPassword(staffPassword),
                FirstName = account.FirstName,
                LastName = account.LastName,
                Role = account.Role,
                IsActive = true
            };

            _dbContext.Users.Add(user);
            created.Add($"{account.Email} ({account.Role})");
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Staff accounts seeded.",
            accounts = created
        });
    }
}
