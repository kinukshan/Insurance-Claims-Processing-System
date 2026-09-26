using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.RiskAssessment.DTOs;
using InsuranceClaims.Application.RiskAssessment.Interfaces;
using InsuranceClaims.Application.RiskAssessment.Validators;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.RiskAssessment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// Risk assessment and fraud flagging endpoints — Component C (Member 3).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ClaimsAdjuster,Underwriter,Admin")]
public class RiskAssessmentsController : ControllerBase
{
    private readonly IRiskAssessmentService _riskService;
    private readonly IClaimRepository? _claimRepository;
    private readonly INotificationOrchestrator? _notificationOrchestrator;
    private readonly IUserEmailResolver? _userEmailResolver;
    private readonly ILogger<RiskAssessmentsController> _logger;

    public RiskAssessmentsController(
        IRiskAssessmentService riskService,
        IClaimRepository? claimRepository = null,
        INotificationOrchestrator? notificationOrchestrator = null,
        IUserEmailResolver? userEmailResolver = null,
        ILogger<RiskAssessmentsController>? logger = null)
    {
        _riskService = riskService;
        _claimRepository = claimRepository;
        _notificationOrchestrator = notificationOrchestrator;
        _userEmailResolver = userEmailResolver;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RiskAssessmentsController>.Instance;
    }

    /// <summary>
    /// Get all risk assessments across all claims.
    /// Authoritative source for dashboard summary metrics and complete assessment list.
    /// </summary>
    [HttpGet]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetAllAssessments()
    {
        var result = await _riskService.GetAllAssessmentsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Trigger a risk assessment on a claim.
    /// Combines deterministic rules with optional AI agent analysis.
    /// </summary>
    [HttpPost("{claimId:guid}/assess")]
    // TODO: [Authorize(Roles = "Staff,Admin")] — uncomment when auth is integrated
    public async Task<IActionResult> AssessClaim(Guid claimId, [FromBody] AssessClaimRequest request)
    {
        var errors = AssessClaimRequestValidator.Validate(claimId, request);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        try
        {
            var result = await _riskService.AssessClaimAsync(claimId, request);

            // Await non-authoritative neutral notification if review is indicated
            if (_notificationOrchestrator != null && _userEmailResolver != null && _claimRepository != null)
            {
                try
                {
                    if (result.Recommendation == RiskRecommendation.Escalate ||
                        result.RiskLevel == RiskLevel.High ||
                        result.RiskScore >= 60m)
                    {
                        var claim = await _claimRepository.GetByIdAsync(claimId);
                        if (claim != null)
                        {
                            var email = await _userEmailResolver.GetEmailAsync(claim.PolicyHolderId);
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                var riskKey = $"claim:{claimId}:risk-needs-review:{result.Id}";
                                await _notificationOrchestrator.NotifyAsync(
                                    riskKey,
                                    claim.PolicyHolderId,
                                    email,
                                    claimId,
                                    NotificationType.RiskAssessmentNeedsReview,
                                    claim.ClaimNumber);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-authoritative risk notification failed for claim {ClaimId}", claimId);
                }
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }



    /// <summary>
    /// Get the latest risk assessment for a specific claim.
    /// </summary>
    [HttpGet("{claimId:guid}")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetAssessment(Guid claimId)
    {
        var result = await _riskService.GetAssessmentAsync(claimId);
        if (result == null)
            return NotFound(new { error = $"No risk assessment found for claim {claimId}." });

        return Ok(result);
    }

    /// <summary>
    /// Get all claims that have unresolved fraud flags.
    /// </summary>
    [HttpGet("flagged")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetFlaggedClaims()
    {
        var result = await _riskService.GetFlaggedClaimsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Get fraud case history for a policyholder.
    /// </summary>
    [HttpGet("history/{policyholderId:guid}")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetHistory(Guid policyholderId)
    {
        var result = await _riskService.GetHistoryAsync(policyholderId);
        return Ok(result);
    }

    /// <summary>
    /// Escalate a risk assessment to a fraud case for manual investigation.
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateRequest request)
    {
        var errors = AssessClaimRequestValidator.ValidateEscalation(id, request);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        try
        {
            var result = await _riskService.EscalateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all fraud flags for a specific claim.
    /// </summary>
    [HttpGet("{claimId:guid}/flags")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetFlags(Guid claimId)
    {
        var result = await _riskService.GetFlagsAsync(claimId);
        return Ok(result);
    }

    /// <summary>
    /// Get policyholder-safe review status for a claim.
    /// Does NOT expose internal fraud scores, flags, or detection rules.
    /// </summary>
    [HttpGet("{claimId:guid}/status")]
    // TODO: [Authorize] — any authenticated user
    public async Task<IActionResult> GetPolicyholderStatus(Guid claimId)
    {
        var result = await _riskService.GetPolicyholderStatusAsync(claimId);
        return Ok(result);
    }

    /// <summary>
    /// Update a fraud case (status, notes, resolution, assignment).
    /// </summary>
    [HttpPut("fraud-cases/{id:guid}")]
    // TODO: [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> UpdateFraudCase(Guid id, [FromBody] UpdateFraudCaseRequest request)
    {
        var errors = AssessClaimRequestValidator.ValidateUpdateFraudCase(id, request);
        if (errors.Count > 0)
            return BadRequest(new { errors });

        try
        {
            var result = await _riskService.UpdateFraudCaseAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
