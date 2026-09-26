using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Domain.PolicyManagement;
using InsuranceClaims.Domain.PolicyManagement.Exceptions;
using InsuranceClaims.Domain.Users;

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
        // 2 & 3. Retrieve trusted context from backend — includes claim-status eligibility
        var context = await _contextProvider.GetPayoutContextAsync(claimId)
            ?? throw new InvalidOperationException(
                $"No eligible payout context found for claim '{claimId}'. " +
                "The claim may not exist, may not be approved, or policy data is unavailable.");

        // 4. Duplicate payout protection: existing non-draft payout throws conflict
        var existing = await _repository.GetByClaimIdAsync(claimId);
        if (existing is not null && !existing.CanBeDeleted())
        {
            throw new InvalidOperationException(
                $"A payout already exists for claim '{claimId}' with status '{existing.Status}'.");
        }

        // 5. Policy/claim compatibility pre-check (fails closed)
        if (!PolicyClaimCompatibility.IsCompatible(context.PolicyType, context.ClaimType))
        {
            throw new PolicyClaimCompatibilityException(
                PolicyClaimCompatibility.GetErrorMessage(context.PolicyType, context.ClaimType));
        }

        // 6. Determine Life condition (strictly AND: Life Insurance AND Life claim)
        var isLife = PolicyClaimCompatibility.IsLifeInsurance(context.PolicyType)
                  && PolicyClaimCompatibility.IsLifeClaim(context.ClaimType);

        // 7. Compute effective deductible (defense-in-depth: Life always 0, even if legacy/tampered > 0)
        var effectiveDeductible = isLife ? 0m : context.Deductible;

        // 8, 9, 10. Build candidate payout IN MEMORY
        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            ClaimId = context.ClaimId,
            ApprovedClaimAmount = context.ApprovedClaimAmount,
            CoverageLimit = context.CoverageLimit,
            Deductible = effectiveDeductible,
            Status = PayoutStatus.Draft
        };

        // Deterministic calculation:
        // eligible = min(ApprovedClaimAmount, CoverageLimit)
        // final = eligible - effectiveDeductible (for Life: effectiveDeductible=0 => eligible)
        payout.CalculatePayout();

        // 11. Request deterministic validation from the Validation/Safety Agent
        var validationResult = await _validationAgent.ValidatePayoutProposalAsync(
            new PayoutValidationRequest
            {
                ClaimId = context.ClaimId,
                PolicyType = context.PolicyType,
                ClaimType = context.ClaimType,
                ApprovedClaimAmount = context.ApprovedClaimAmount,
                CoverageLimit = context.CoverageLimit,
                Deductible = effectiveDeductible,
                ProposedPayout = payout.ProposedPayout
            });

        // 12. If deterministic validation fails -> stop immediately (no mutations, draft preserved)
        if (!validationResult.Valid)
        {
            throw new InvalidOperationException(
                $"Payout proposal failed validation: {string.Join("; ", validationResult.Violations)}");
        }

        // 13. Validation passed -> atomic draft replacement and persistence
        // Replaceable draft is only deleted after new candidate has fully passed validation
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
    public async Task<PayoutDto?> GetByIdAsync(Guid id, Guid? userId = null, Role? role = null)
    {
        var payout = await _repository.GetByIdAsync(id);
        if (payout is null) return null;

        if (role == Role.Policyholder && userId.HasValue)
        {
            if (payout.Claim == null || payout.Claim.PolicyHolderId != userId.Value)
            {
                throw new UnauthorizedAccessException("You do not have permission to view this payout.");
            }
        }

        var dto = MapToDto(payout);
        dto.ClaimNumber ??= await GetClaimNumberAsync(payout.ClaimId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PayoutDto?> GetByClaimIdAsync(Guid claimId, Guid? userId = null, Role? role = null)
    {
        var payout = await _repository.GetByClaimIdAsync(claimId);
        if (payout is null) return null;

        if (role == Role.Policyholder && userId.HasValue)
        {
            if (payout.Claim == null || payout.Claim.PolicyHolderId != userId.Value)
            {
                throw new UnauthorizedAccessException("You do not have permission to view this payout.");
            }
        }

        var dto = MapToDto(payout);
        dto.ClaimNumber ??= await GetClaimNumberAsync(claimId);
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
            dto.ClaimNumber ??= await GetClaimNumberAsync(item.ClaimId);
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
    public async Task<PaginatedResult<PayoutDto>> GetMyPayoutsAsync(Guid policyholderId, PayoutHistoryQueryDto query)
    {
        var (items, totalCount) = await _repository.GetPagedByPolicyholderAsync(
            policyholderId, query.Page, query.PageSize, query.StatusFilter, query.SortBy, query.SortDescending);

        var dtos = new List<PayoutDto>(items.Count);
        foreach (var item in items)
        {
            var dto = MapToDto(item);
            dto.ClaimNumber ??= await GetClaimNumberAsync(item.ClaimId);
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

        // PREVENT ZERO-VALUE PAYOUTS FROM EVER EXECUTING OR REACHING PAYMENT GATEWAYS
        if (payout.FinalPayout <= 0)
        {
            throw new InvalidOperationException(
                "Cannot execute a zero-value payout. Eligible claim amount does not exceed the policy deductible.");
        }

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
        var eligible = Math.Min(payout.ApprovedClaimAmount, payout.CoverageLimit);
        string? explanation = null;
        if (payout.FinalPayout <= 0 && payout.Deductible > 0 && eligible <= payout.Deductible)
        {
            explanation = "Your eligible claim amount does not exceed your policy deductible. No insurance payout is payable for this claim.";
        }

        return new PayoutDto
        {
            Id = payout.Id,
            ClaimId = payout.ClaimId,
            ClaimNumber = payout.Claim?.ClaimNumber,
            ApprovedClaimAmount = payout.ApprovedClaimAmount,
            CoverageLimit = payout.CoverageLimit,
            Deductible = payout.Deductible,
            ProposedPayout = payout.ProposedPayout,
            FinalPayout = payout.FinalPayout,
            Explanation = explanation,
            Status = payout.Status,
            ApprovedBy = payout.ApprovedBy,
            ApprovalTimestamp = payout.ApprovalTimestamp,
            PaymentReference = payout.PaymentReference,
            PaymentProvider = payout.PaymentTransactions?.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.Provider,
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
