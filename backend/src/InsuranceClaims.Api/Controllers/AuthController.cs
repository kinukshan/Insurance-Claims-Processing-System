using System.Security.Claims;
using InsuranceClaims.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Authentication endpoints — shared infrastructure, not a student business component.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// POST /api/auth/register — Register a new Policyholder account.
    /// Public — no authentication required.
    /// Role is always Policyholder; cannot be escalated.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Created(string.Empty, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/auth/login — Authenticate a user and return a JWT.
    /// Public — no authentication required.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
    }

    /// <summary>
    /// GET /api/auth/me — Get the current authenticated user's profile.
    /// Requires authentication.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token." });

        var profile = await _authService.GetCurrentUserAsync(userId);
        if (profile == null)
            return NotFound(new { error = "User not found." });

        return Ok(profile);
    }
}
