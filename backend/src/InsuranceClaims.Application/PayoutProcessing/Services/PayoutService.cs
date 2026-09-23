using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.Services;

/// <summary>
/// Payout business logic service.
///
/// All authoritative financial values are retrieved from IPayoutContextProvider.
/// Reviewer identity is passed from the controller (derived from auth context).
/// </summary>
public class PayoutService : IPayoutService
{
    private readonly IPayoutRepository _repository;
    private readonly IPayoutContextProvider _contextProvider;
    private readonly IPayoutValidationAgentGateway _validationAgent;

    public PayoutService(
        IPayoutRepository repository,
        IPayoutContextProvider contextProvider,
        IPayoutValidationAgentGateway validationAgent)
    {
        _repository = repository;
        _contextProvider = contextProvider;
        _validationAgent = validationAgent;
    }

    /// <inheritdoc />
    public async Task<PayoutDto> CalculatePayoutAsync(Guid claimId)
    {
        // Retrieve trusted context from backend — never from client inputs
        var context = await _contextProvider.GetPayoutContextAsync(claimId)
            ?? throw new InvalidOperationException(
                $"No eligible payout context found for claim '{claimId}'. " +
                "The claim may not exist, may not be approved, or policy data is unavailable.");

        // Check if a payout already exists for this claim
        var existing = await _repository.GetByClaimIdAsync(claimId);
        if (existing is not null && !existing.CanBeDeleted())
        {
            throw new InvalidOperationException(
                $"A payout already exists for claim '{claimId}' with status '{existing.Status}'.");
        }

        // Build payout from trusted data
        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = context.ClaimId,
            ApprovedClaimAmount = context.ApprovedClaimAmount,
            CoverageLimit = context.CoverageLimit,
            Deductible = context.Deductible,
            Status = PayoutStatus.Draft
        };

        // Deterministic calculation
        payout.CalculatePayout();

        // Request validation from the Validation/Safety Agent
        var validationResult = await _validationAgent.ValidatePayoutProposalAsync(
            new PayoutValidationRequest
            {
                ClaimId = context.ClaimId,
                PolicyType = context.PolicyType,
                ClaimType = context.ClaimType,
                ApprovedClaimAmount = context.ApprovedClaimAmount,
                CoverageLimit = context.CoverageLimit,
                Deductible = context.Deductible,
                ProposedPayout = payout.ProposedPayout
            });

        if (!validationResult.Valid)
        {
            throw new InvalidOperationException(
                $"Payout proposal failed validation: {string.Join("; ", validationResult.Violations)}");
        }

        // If a replaceable draft exists, delete it first
        if (existing is not null && existing.CanBeDeleted())
        {
            await _repository.DeleteAsync(existing);
        }

        // Submit for approval automatically if validation requires it
        if (validationResult.RequiresHumanApproval)
        {
            payout.SubmitForApproval();
        }

        await _repository.AddAsync(payout);

        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(claimId);
        dto.ValidationResult = MapValidationResult(validationResult);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto?> GetByIdAsync(Guid id)
    {
        var payout = await _repository.GetByIdAsync(id);
        if (payout is null) return null;
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto?> GetByClaimIdAsync(Guid claimId)
    {
        var payout = await _repository.GetByClaimIdAsync(claimId);
        if (payout is null) return null;
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(claimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<PayoutDto>> GetHistoryAsync(PayoutHistoryQueryDto query)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            query.Page, query.PageSize, query.StatusFilter, query.SortBy, query.SortDescending);

        var dtos = new List<PayoutDto>(items.Count);
        foreach (var item in items)
        {
            var dto = MapToDto(item);
            dto.ClaimNumber = await GetClaimNumberAsync(item.ClaimId);
            dtos.Add(dto);
        }

        return new PaginatedResult<PayoutDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<PayoutDto> UpdatePayoutAsync(Guid id)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        if (payout.Status != PayoutStatus.Draft && payout.Status != PayoutStatus.RevisionRequested)
        {
            throw new InvalidOperationException(
                $"Cannot update payout in status '{payout.Status}'. Must be Draft or RevisionRequested.");
        }

        // Re-retrieve trusted context and recalculate
        var context = await _contextProvider.GetPayoutContextAsync(payout.ClaimId)
            ?? throw new InvalidOperationException(
                $"No eligible payout context found for claim '{payout.ClaimId}'.");

        payout.ApprovedClaimAmount = context.ApprovedClaimAmount;
        payout.CoverageLimit = context.CoverageLimit;
        payout.Deductible = context.Deductible;
        payout.CalculatePayout();

        // Reset to Draft if it was RevisionRequested
        if (payout.Status == PayoutStatus.RevisionRequested)
        {
            payout.Status = PayoutStatus.Draft;
        }

        await _repository.UpdateAsync(payout);
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto> ApprovePayoutAsync(
        Guid id, string comments, Guid reviewerId, string reviewerName)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        payout.Approve(reviewerId, reviewerName, comments);

        await _repository.UpdateAsync(payout);
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto> RejectPayoutAsync(
        Guid id, string comments, Guid reviewerId, string reviewerName)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        payout.Reject(reviewerId, reviewerName, comments);

        await _repository.UpdateAsync(payout);
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto> RequestRevisionAsync(
        Guid id, string comments, Guid reviewerId, string reviewerName)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        payout.RequestRevision(reviewerId, reviewerName, comments);

        await _repository.UpdateAsync(payout);
        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto> ExecutePayoutAsync(Guid id)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        // CRITICAL: A payout must NOT execute before valid human approval
        if (!Payout.IsValidTransition(payout.Status, PayoutStatus.Processing))
        {
            throw new InvalidOperationException(
                $"Cannot execute payout from status '{payout.Status}'. Must be Approved.");
        }

        payout.Status = PayoutStatus.Processing;
        await _repository.UpdateAsync(payout);

        // NOTE: Actual payment gateway call is handled externally.
        // The controller or an orchestrator will call IPaymentGateway after this.
        // This method only transitions the state to Processing.

        var dto = MapToDto(payout);
        dto.ClaimNumber = await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task DeletePayoutAsync(Guid id)
    {
        var payout = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Payout '{id}' not found.");

        if (!payout.CanBeDeleted())
        {
            throw new InvalidOperationException(
                $"Cannot delete payout in status '{payout.Status}'. " +
                "Only Draft or RevisionRequested payouts can be deleted.");
        }

        await _repository.DeleteAsync(payout);
    }

    // ── Mapping ──────────────────────────────────────────────────────

    private static PayoutDto MapToDto(Payout payout)
    {
        return new PayoutDto
        {
            Id = payout.Id,
            ClaimId = payout.ClaimId,
            ApprovedClaimAmount = payout.ApprovedClaimAmount,
            CoverageLimit = payout.CoverageLimit,
            Deductible = payout.Deductible,
            ProposedPayout = payout.ProposedPayout,
            FinalPayout = payout.FinalPayout,
            Status = payout.Status,
            ApprovedBy = payout.ApprovedBy,
            ApprovalTimestamp = payout.ApprovalTimestamp,
            PaymentReference = payout.PaymentReference,
            CreatedAt = payout.CreatedAt,
            UpdatedAt = payout.UpdatedAt,
            Approvals = payout.Approvals.Select(a => new PayoutApprovalDto
            {
                Id = a.Id,
                PayoutId = a.PayoutId,
                Decision = a.Decision,
                ReviewerName = a.ReviewerName,
                Comments = a.Comments,
                DecisionTimestamp = a.DecisionTimestamp
            }).ToList()
        };
    }

    private static PayoutValidationResultDto MapValidationResult(PayoutValidationResult result)
    {
        return new PayoutValidationResultDto
        {
            Valid = result.Valid,
            Violations = result.Violations,
            RequiresHumanApproval = result.RequiresHumanApproval,
            AgentId = result.AgentId,
            Summary = result.Summary,
            AiUsed = result.AiUsed,
            AiProvider = result.AiProvider,
            AiModel = result.AiModel,
            ReasoningSummary = result.ReasoningSummary,
            FallbackUsed = result.FallbackUsed
        };
    }

    /// <summary>
    /// Attempt to resolve the claim number for display purposes.
    /// Returns null if the claim is not found (non-critical).
    /// </summary>
    private async Task<string?> GetClaimNumberAsync(Guid claimId)
    {
        // Use the context provider to check if claim exists, but we need
        // the claim number which the context doesn't expose.
        // For now, we'll return null — the frontend uses ClaimId as fallback.
        // A future improvement could add ClaimNumber to PayoutContext.
        return null;
    }
}
