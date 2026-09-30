using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Interfaces;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Policy management endpoints — Component A (Member 1).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PoliciesController : ControllerBase
{
    private readonly IPolicyService _policyService;

    public PoliciesController(IPolicyService policyService)
    {
        _policyService = policyService;
    }

    /// <summary>
    /// GET /api/policies — Retrieve policies.
    /// For Policyholder role: returns only policies belonging to the authenticated user.
    /// For Staff roles: returns all policies.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetAll()
    {
        var role = GetCurrentUserRole();
        if (role == Role.Policyholder)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var userPolicies = await _policyService.GetByPolicyholderIdAsync(userId);
            return Ok(userPolicies);
        }

        var policies = await _policyService.GetAllAsync();
        return Ok(policies);
    }

    /// <summary>
    /// GET /api/policies/{id} — Retrieve a policy by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PolicyDto>> GetById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var policy = await _policyService.GetByIdAsync(id, userId, role);
            if (policy == null)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return Ok(policy);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET /api/policies/policyholder/{policyholderId} — Retrieve all policies for a policyholder.
    /// </summary>
    [HttpGet("policyholder/{policyholderId:guid}")]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetByPolicyholder(Guid policyholderId)
    {
        var role = GetCurrentUserRole();
        if (role == Role.Policyholder)
        {
            var userId = GetCurrentUserId();
            if (policyholderId != userId)
                return Forbid();
        }

        var policies = await _policyService.GetByPolicyholderIdAsync(policyholderId);
        return Ok(policies);
    }

    /// <summary>
    /// GET /api/policies/my — Retrieve policies for the authenticated Policyholder.
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetMyPolicies()
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var policies = await _policyService.GetByPolicyholderIdAsync(userId);
        return Ok(policies);
    }

    /// <summary>
    /// POST /api/policies — Create a new policy.
    /// For Policyholder role: PolicyholderId is derived from JWT (cannot be overridden).
    /// For Admin/Underwriter: PolicyholderId from request body is used.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PolicyDto>> Create([FromBody] CreatePolicyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // For Policyholder role, override PolicyholderId with authenticated user's ID
            var role = GetCurrentUserRole();
            if (role == Role.Policyholder)
            {
                var userId = GetCurrentUserId();
                if (userId == Guid.Empty)
                    return Unauthorized();

                // Override PolicyholderId with the authenticated user's ID
                dto.PolicyholderId = userId;
            }

            var created = await _policyService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/policies/{id} — Update an existing policy.
    /// Only Underwriters and Admins are permitted to update policies.
    /// Underwriters can edit coverage limit, expiry date, and exclusions.
    /// Admins can additionally change policy status with validated transitions.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Underwriter,Admin")]
    public async Task<ActionResult<PolicyDto>> Update(Guid id, [FromBody] UpdatePolicyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty && User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var role = GetCurrentUserRole();
        if (role != Role.Underwriter && role != Role.Admin)
            return Forbid();

        try
        {
            var updated = await _policyService.UpdateAsync(id, dto, userId, role);
            if (updated == null)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/policies/{id} — Delete a draft policy.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var deleted = await _policyService.DeleteAsync(id, userId, role);
            if (!deleted)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this item." });
        }
        catch (InsuranceClaims.Application.Common.Exceptions.ConflictException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete the item. Please try again." });
        }
    }

    /// <summary>
    /// POST /api/policies/{id}/calculate-premium — Calculate premium for a policy.
    /// </summary>
    [HttpPost("{id:guid}/calculate-premium")]
    public async Task<ActionResult<PremiumCalculationResultDto>> CalculatePremium(Guid id)
    {
        try
        {
            var role = GetCurrentUserRole();
            if (role == Role.Policyholder)
            {
                var userId = GetCurrentUserId();
                var policy = await _policyService.GetByIdAsync(id, userId, role);
                if (policy == null)
                    return NotFound(new { message = $"Policy with ID '{id}' not found." });
            }

            var result = await _policyService.CalculatePremiumAsync(id);
            if (result == null)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET /api/policies/{id}/coverage — Get coverage details for a policy.
    /// </summary>
    [HttpGet("{id:guid}/coverage")]
    public async Task<ActionResult<IEnumerable<PolicyCoverageDto>>> GetCoverage(Guid id)
    {
        try
        {
            var role = GetCurrentUserRole();
            if (role == Role.Policyholder)
            {
                var userId = GetCurrentUserId();
                var policy = await _policyService.GetByIdAsync(id, userId, role);
                if (policy == null)
                    return NotFound(new { message = $"Policy with ID '{id}' not found." });
            }

            var coverage = await _policyService.GetCoverageAsync(id);
            return Ok(coverage);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// POST /api/policies/{id}/renew — Renew a policy.
    /// </summary>
    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult<PolicyRenewalResultDto>> Renew(Guid id)
    {
        try
        {
            var role = GetCurrentUserRole();
            if (role == Role.Policyholder)
            {
                var userId = GetCurrentUserId();
                var policy = await _policyService.GetByIdAsync(id, userId, role);
                if (policy == null)
                    return NotFound(new { message = $"Policy with ID '{id}' not found." });
            }

            var result = await _policyService.RenewPolicyAsync(id);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// POST /api/policies/validate-expiry — Validate and update expired policies.
    /// </summary>
    [HttpPost("validate-expiry")]
    [Authorize(Roles = "Underwriter,Admin")]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> ValidateExpiry()
    {
        var expired = await _policyService.ValidateExpiryAsync();
        return Ok(expired);
    }

    /// <summary>
    /// Extracts the current user's ID from JWT claims.
    /// Falls back to X-User-Id header in Development only.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("nameid")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
            return userId;

        // Dev fallback: allow X-User-Id header for testing without auth
        if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            Request.Headers.TryGetValue("X-User-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var headerUserId))
            return headerUserId;

        return Guid.Empty;
    }

    /// <summary>
    /// Extracts the current user's role from JWT claims.
    /// Falls back to X-User-Role header in Development only.
    /// Defaults to Policyholder if not found or cannot be parsed.
    /// </summary>
    private Role GetCurrentUserRole()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(roleClaim) && Enum.TryParse<Role>(roleClaim, ignoreCase: true, out var role))
            return role;

        // Dev fallback: allow X-User-Role header for testing without auth
        if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            Request.Headers.TryGetValue("X-User-Role", out var headerValue) &&
            Enum.TryParse<Role>(headerValue, ignoreCase: true, out var headerRole))
            return headerRole;

        return Role.Policyholder;
    }
}
