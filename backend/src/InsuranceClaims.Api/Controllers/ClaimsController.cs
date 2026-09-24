using System.Security.Claims;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Claims submission and document verification endpoints — Component B (Member 2).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _claimService;

    public ClaimsController(IClaimService claimService)
    {
        _claimService = claimService;
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

    /// <summary>
    /// POST /api/claims — Create a new claim (starts as Draft).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClaimResponseDto>> CreateClaim([FromBody] CreateClaimDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            var role = GetCurrentUserRole();
            var result = await _claimService.CreateClaimAsync(userId, dto, role);
            return CreatedAtAction(nameof(GetClaim), new { id = result.Id }, result);
        }
        catch (PolicyClaimCompatibilityException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = ex.Message.Split("; ") });
        }
    }

    /// <summary>
    /// GET /api/claims/{id} — Get a claim by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimResponseDto>> GetClaim(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.GetClaimAsync(id, userId, role);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET /api/claims/{id}/document-requirements — Get deterministic required documents and upload status.
    /// </summary>
    [HttpGet("{id:guid}/document-requirements")]
    public async Task<ActionResult<ClaimDocumentRequirementsDto>> GetDocumentRequirements(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.GetDocumentRequirementsAsync(id, userId, role);
            if (result == null) return NotFound(new { message = $"Claim with ID '{id}' was not found." });
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// GET /api/claims/my-claims — Get current user's claims.
    /// </summary>
    [HttpGet("my-claims")]
    public async Task<ActionResult<List<ClaimSummaryDto>>> GetMyClaims()
    {
        var userId = GetCurrentUserId();
        var results = await _claimService.GetMyClaimsAsync(userId);
        return Ok(results);
    }

    /// <summary>
    /// GET /api/claims — Get all claims (scoped to policyholder if caller is Policyholder, otherwise staff view) with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClaimSummaryDto>>> GetAllClaims(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var role = GetCurrentUserRole();
        Guid? policyHolderId = null;

        if (role == Role.Policyholder)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
                return Unauthorized();

            policyHolderId = userId;
        }

        var results = await _claimService.GetAllClaimsAsync(status, search, policyHolderId);
        return Ok(results);
    }

    /// <summary>
    /// PUT /api/claims/{id} — Update a draft claim.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClaimResponseDto>> UpdateClaim(Guid id, [FromBody] UpdateClaimDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _claimService.UpdateClaimAsync(id, userId, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = ex.Message.Split("; ") });
        }
    }

    /// <summary>
    /// DELETE /api/claims/{id} — Hard delete a draft claim.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteClaim(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.DeleteClaimAsync(id, userId, role);
            if (!result) return NotFound(new { message = $"Claim with ID '{id}' not found." });
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this item." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete the item. Please try again." });
        }
    }

    /// <summary>
    /// POST /api/claims/{id}/withdraw — Withdraw a submitted or under-review claim.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<ClaimResponseDto>> WithdrawClaim(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.WithdrawClaimAsync(id, userId, role);
            if (result == null) return NotFound(new { message = $"Claim with ID '{id}' not found." });
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Claim with ID '{id}' not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this item." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete the item. Please try again." });
        }
    }

    /// <summary>
    /// POST /api/claims/{id}/submit — Submit a draft claim.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ClaimResponseDto>> SubmitClaim(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _claimService.SubmitClaimAsync(id, userId);
            if (result == null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/claims/{id}/documents — Upload a document to a claim.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<ClaimDocumentDto>> UploadDocument(Guid id, IFormFile file, [FromForm] string documentType)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            var dto = new UploadDocumentDto(
                DocumentType: documentType,
                FileName: file.FileName,
                ContentType: file.ContentType,
                FileSize: file.Length,
                FileStream: file.OpenReadStream()
            );

            var result = await _claimService.AddDocumentAsync(id, userId, role, dto);
            return CreatedAtAction(nameof(GetDocuments), new { id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/claims/{id}/documents — Get documents for a claim.
    /// </summary>
    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<List<ClaimDocumentDto>>> GetDocuments(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var results = await _claimService.GetDocumentsAsync(id, userId, role);
            return Ok(results);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// DELETE /api/claims/{claimId}/documents/{documentId} — Delete a document from a claim.
    /// </summary>
    [HttpDelete("{claimId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid claimId, Guid documentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.DeleteDocumentAsync(claimId, documentId, userId, role);
            if (!result) return NotFound(new { message = "Document not found." });
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete this item." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Unable to delete the item. Please try again." });
        }
    }

    /// <summary>
    /// POST /api/claims/{id}/validate-coverage — Validate claim against policy coverage.
    /// </summary>
    [HttpPost("{id:guid}/validate-coverage")]
    public async Task<ActionResult<CoverageValidationResultDto>> ValidateCoverage(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.ValidateCoverageAsync(id, userId, role);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// POST /api/claims/{id}/start-workflow — Start the document verification workflow.
    /// </summary>
    [HttpPost("{id:guid}/start-workflow")]
    public async Task<ActionResult<DocumentVerificationResultDto>> StartWorkflow(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _claimService.VerifyDocumentsAsync(id, userId, role);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

