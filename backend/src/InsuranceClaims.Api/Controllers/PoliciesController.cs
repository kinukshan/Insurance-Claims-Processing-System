using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using InsuranceClaims.Application.PolicyManagement.DTOs;
using InsuranceClaims.Application.PolicyManagement.Interfaces;

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
    /// GET /api/policies — Retrieve all policies.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetAll()
    {
        var policies = await _policyService.GetAllAsync();
        return Ok(policies);
    }

    /// <summary>
    /// GET /api/policies/{id} — Retrieve a policy by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PolicyDto>> GetById(Guid id)
    {
        var policy = await _policyService.GetByIdAsync(id);
        if (policy == null)
            return NotFound(new { message = $"Policy with ID '{id}' not found." });

        return Ok(policy);
    }

    /// <summary>
    /// GET /api/policies/policyholder/{policyholderId} — Retrieve all policies for a policyholder.
    /// </summary>
    [HttpGet("policyholder/{policyholderId:guid}")]
    public async Task<ActionResult<IEnumerable<PolicyDto>>> GetByPolicyholder(Guid policyholderId)
    {
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
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim == "Policyholder")
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
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PolicyDto>> Update(Guid id, [FromBody] UpdatePolicyDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _policyService.UpdateAsync(id, dto);
            if (updated == null)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return Ok(updated);
        }
        catch (ArgumentException ex)
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
            var deleted = await _policyService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Policy with ID '{id}' not found." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/policies/{id}/calculate-premium — Calculate premium for a policy.
    /// </summary>
    [HttpPost("{id:guid}/calculate-premium")]
    public async Task<ActionResult<PremiumCalculationResultDto>> CalculatePremium(Guid id)
    {
        var result = await _policyService.CalculatePremiumAsync(id);
        if (result == null)
            return NotFound(new { message = $"Policy with ID '{id}' not found." });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/policies/{id}/coverage — Get coverage details for a policy.
    /// </summary>
    [HttpGet("{id:guid}/coverage")]
    public async Task<ActionResult<IEnumerable<PolicyCoverageDto>>> GetCoverage(Guid id)
    {
        var coverage = await _policyService.GetCoverageAsync(id);
        return Ok(coverage);
    }

    /// <summary>
    /// POST /api/policies/{id}/renew — Renew a policy.
    /// </summary>
    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult<PolicyRenewalResultDto>> Renew(Guid id)
    {
        var result = await _policyService.RenewPolicyAsync(id);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// POST /api/policies/validate-expiry — Validate and update expired policies.
    /// </summary>
    [HttpPost("validate-expiry")]
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
                       ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
            return userId;

        // Dev fallback: allow X-User-Id header for testing without auth
        if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true &&
            Request.Headers.TryGetValue("X-User-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var headerUserId))
            return headerUserId;

        return Guid.Empty;
    }
}
