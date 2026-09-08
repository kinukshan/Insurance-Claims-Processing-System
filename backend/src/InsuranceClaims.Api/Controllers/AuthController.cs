using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Authentication endpoints — shared infrastructure, not a student business component.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // TODO: Inject authentication service

    [HttpPost("login")]
    public IActionResult Login()
    {
        // TODO: Implement login
        return Ok(new { message = "Login endpoint — not yet implemented" });
    }

    [HttpPost("register")]
    public IActionResult Register()
    {
        // TODO: Implement registration
        return Ok(new { message = "Register endpoint — not yet implemented" });
    }
}
