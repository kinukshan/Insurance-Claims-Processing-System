using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.DTOs;

/// <summary>
/// Response DTO for payout data including calculation breakdown.
/// </summary>
public class PayoutDto
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }

    // ── Calculation breakdown ────────────────────────────────────────
    public decimal ApprovedClaimAmount { get; set; }
    public decimal CoverageLimit { get; set; }
    public decimal Deductible { get; set; }
    public decimal ProposedPayout { get; set; }
    public decimal FinalPayout { get; set; }

    // ── Status ───────────────────────────────────────────────────────
    public PayoutStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();

    // ── Approval ─────────────────────────────────────────────────────
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalTimestamp { get; set; }

    // ── Payment ──────────────────────────────────────────────────────
    public string? PaymentReference { get; set; }

    // ── Audit ────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Approvals history ────────────────────────────────────────────
    public List<PayoutApprovalDto> Approvals { get; set; } = new();
}

/// <summary>
/// Response DTO for approval audit records.
/// </summary>
public class PayoutApprovalDto
{
    public Guid Id { get; set; }
    public Guid PayoutId { get; set; }
    public ApprovalDecisionType Decision { get; set; }
    public string DecisionDisplay => Decision.ToString();
    public string ReviewerName { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public DateTime DecisionTimestamp { get; set; }
}

/// <summary>
/// Request DTO for approval actions (approve/reject/revision).
/// Reviewer identity is derived from the authenticated server context — not from this DTO.
/// Only decision and comments come from the client.
/// </summary>
public class PayoutApprovalRequestDto
{
    /// <summary>Reviewer comments/reason for decision.</summary>
    public string Comments { get; set; } = string.Empty;
}

/// <summary>
/// Query parameters for paginated, filtered, sorted payout history.
/// </summary>
public class PayoutHistoryQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public PayoutStatus? StatusFilter { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Paginated response wrapper for payout history.
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
