using System.Security.Claims;
using InsuranceClaims.Application.ClaimsManagement.DTOs;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
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

    /// <summary>
    /// POST /api/claims — Create a new claim (starts as Draft).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClaimResponseDto>> CreateClaim([FromBody] CreateClaimDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _claimService.CreateClaimAsync(userId, dto);
            return CreatedAtAction(nameof(GetClaim), new { id = result.Id }, result);
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
            var result = await _claimService.GetClaimAsync(id, userId);
            if (result == null) return NotFound();
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
    /// GET /api/claims — Get all claims (staff view) with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClaimSummaryDto>>> GetAllClaims(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null)
    {
        var results = await _claimService.GetAllClaimsAsync(status, search);
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
    /// DELETE /api/claims/{id} — Withdraw a draft claim (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteClaim(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _claimService.DeleteClaimAsync(id, userId);
            if (!result) return NotFound();
            return NoContent();
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

            var dto = new UploadDocumentDto(
                DocumentType: documentType,
                FileName: file.FileName,
                ContentType: file.ContentType,
                FileSize: file.Length,
                FileStream: file.OpenReadStream()
            );

            var result = await _claimService.AddDocumentAsync(id, userId, dto);
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
            var results = await _claimService.GetDocumentsAsync(id, userId);
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
    /// POST /api/claims/{id}/validate-coverage — Validate claim against policy coverage.
    /// </summary>
    [HttpPost("{id:guid}/validate-coverage")]
    public async Task<ActionResult<CoverageValidationResultDto>> ValidateCoverage(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _claimService.ValidateCoverageAsync(id, userId);
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
            var result = await _claimService.VerifyDocumentsAsync(id, userId);
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

