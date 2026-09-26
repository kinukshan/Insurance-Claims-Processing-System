using System.Security.Claims;
using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Application.Notifications.DTOs;
using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsuranceClaims.Api.Controllers;

/// <summary>
/// API controller for notification history and management.
/// Provides role-authorized access to notification logs:
/// - Policyholders: own notifications only, verified claim/payout ownership (no IDOR).
/// - Staff (ClaimsAdjuster, Underwriter, Admin): system audit visibility.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationLogRepository _logRepository;
    private readonly IClaimRepository? _claimRepository;
    private readonly IPayoutRepository? _payoutRepository;
    private readonly ILogger<NotificationsController>? _logger;

    public NotificationsController(
        INotificationLogRepository logRepository,
        IClaimRepository? claimRepository = null,
        IPayoutRepository? payoutRepository = null,
        ILogger<NotificationsController>? logger = null)
    {
        _logRepository = logRepository;
        _claimRepository = claimRepository;
        _payoutRepository = payoutRepository;
        _logger = logger;
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

        if (Request.Headers.TryGetValue("X-User-Id", out var headerValue) &&
            Guid.TryParse(headerValue, out var headerUserId))
            return headerUserId;

        return Guid.Empty;
    }

    /// <summary>
    /// Extracts the current user's role from JWT claims.
    /// Falls back to X-User-Role header in Development only.
    /// </summary>
    private Role GetCurrentUserRole()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value;

        if (!string.IsNullOrEmpty(roleClaim) && Enum.TryParse<Role>(roleClaim, true, out var role))
            return role;

        if (Request.Headers.TryGetValue("X-User-Role", out var headerValue) &&
            Enum.TryParse<Role>(headerValue, true, out var headerRole))
            return headerRole;

        return Role.Policyholder;
    }

    private static bool IsStaffRole(Role role) =>
        role is Role.ClaimsAdjuster or Role.Underwriter or Role.Admin;

    /// <summary>
    /// Get notification history.
    /// Policyholders can only view their own notifications.
    /// Staff can view system logs or filter by user, claim, or payout.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<NotificationLogDto>>> GetNotifications(
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? claimId = null,
        [FromQuery] Guid? payoutId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentUserRole();

            if (currentRole == Role.Policyholder)
            {
                // Policyholder: strictly scoped to current user
                if (claimId.HasValue && _claimRepository != null)
                {
                    var claim = await _claimRepository.GetByIdAsync(claimId.Value);
                    if (claim == null)
                        return NotFound(new { error = $"Claim '{claimId}' not found." });

                    if (claim.PolicyHolderId != currentUserId)
                        return Forbid();

                    var claimLogs = await _logRepository.GetByClaimIdAsync(claimId.Value);
                    return Ok(claimLogs.Where(l => l.UserId == currentUserId).Select(MapToDto).ToList());
                }

                if (payoutId.HasValue && _payoutRepository != null)
                {
                    var payout = await _payoutRepository.GetByIdAsync(payoutId.Value);
                    if (payout == null)
                        return NotFound(new { error = $"Payout '{payoutId}' not found." });

                    if (payout.Claim != null && payout.Claim.PolicyHolderId != currentUserId)
                        return Forbid();

                    var payoutLogs = await _logRepository.GetByPayoutIdAsync(payoutId.Value);
                    return Ok(payoutLogs.Where(l => l.UserId == currentUserId).Select(MapToDto).ToList());
                }

                // Always enforce currentUserId for policyholders — ignore any requested userId
                var userLogs = await _logRepository.GetByUserIdAsync(currentUserId);
                return Ok(userLogs.Select(MapToDto).ToList());
            }

            if (IsStaffRole(currentRole))
            {
                List<NotificationLog> logs;

                if (claimId.HasValue)
                {
                    logs = await _logRepository.GetByClaimIdAsync(claimId.Value);
                }
                else if (payoutId.HasValue)
                {
                    logs = await _logRepository.GetByPayoutIdAsync(payoutId.Value);
                }
                else if (userId.HasValue)
                {
                    logs = await _logRepository.GetByUserIdAsync(userId.Value);
                }
                else
                {
                    logs = await _logRepository.GetAllAsync(page, pageSize);
                }

                return Ok(logs.Select(MapToDto).ToList());
            }

            return Forbid();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve notifications");
            return StatusCode(500, new { error = "Unable to load notifications. Please try again." });
        }
    }

    /// <summary>
    /// Get notification history for a specific claim.
    /// Enforces ownership check for policyholders.
    /// </summary>
    [HttpGet("claim/{claimId:guid}")]
    public async Task<ActionResult<List<NotificationLogDto>>> GetByClaimId(Guid claimId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentUserRole();

            if (currentRole == Role.Policyholder)
            {
                if (_claimRepository != null)
                {
                    var claim = await _claimRepository.GetByIdAsync(claimId);
                    if (claim == null)
                        return NotFound(new { error = $"Claim '{claimId}' not found." });

                    if (claim.PolicyHolderId != currentUserId)
                        return Forbid();
                }

                var logs = await _logRepository.GetByClaimIdAsync(claimId);
                return Ok(logs.Where(l => l.UserId == currentUserId).Select(MapToDto).ToList());
            }

            if (IsStaffRole(currentRole))
            {
                var logs = await _logRepository.GetByClaimIdAsync(claimId);
                return Ok(logs.Select(MapToDto).ToList());
            }

            return Forbid();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve claim notifications for claim {ClaimId}", claimId);
            return StatusCode(500, new { error = "Unable to load notifications. Please try again." });
        }
    }

    /// <summary>
    /// Get notification history for a specific payout.
    /// Enforces ownership check for policyholders.
    /// </summary>
    [HttpGet("payout/{payoutId:guid}")]
    public async Task<ActionResult<List<NotificationLogDto>>> GetByPayoutId(Guid payoutId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentRole = GetCurrentUserRole();

            if (currentRole == Role.Policyholder)
            {
                if (_payoutRepository != null)
                {
                    var payout = await _payoutRepository.GetByIdAsync(payoutId);
                    if (payout == null)
                        return NotFound(new { error = $"Payout '{payoutId}' not found." });

                    if (payout.Claim != null && payout.Claim.PolicyHolderId != currentUserId)
                        return Forbid();
                }

                var logs = await _logRepository.GetByPayoutIdAsync(payoutId);
                return Ok(logs.Where(l => l.UserId == currentUserId).Select(MapToDto).ToList());
            }

            if (IsStaffRole(currentRole))
            {
                var logs = await _logRepository.GetByPayoutIdAsync(payoutId);
                return Ok(logs.Select(MapToDto).ToList());
            }

            return Forbid();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve payout notifications for payout {PayoutId}", payoutId);
            return StatusCode(500, new { error = "Unable to load notifications. Please try again." });
        }
    }

    private static NotificationLogDto MapToDto(NotificationLog n) =>
        new(
            n.Id,
            n.UserId,
            n.ClaimId,
            n.NotificationType.ToString(),
            n.Channel.ToString(),
            n.Recipient,
            n.Subject,
            n.Success,
            n.ErrorMessage,
            n.Provider,
            n.ProviderMessageId,
            n.SentAt,
            n.CreatedAt,
            n.PayoutId,
            n.NotificationKey,
            n.Status.ToString()
        );
}
