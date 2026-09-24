using InsuranceClaims.Application.PayoutProcessing.DTOs;
using InsuranceClaims.Domain.Users;

namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Service interface for payout business operations.
/// </summary>
public interface IPayoutService
{
    /// <summary>
    /// Calculate and create a payout proposal for the given claim.
    /// All financial inputs come from IPayoutContextProvider — not from the client.
    /// </summary>
    Task<PayoutDto> CalculatePayoutAsync(Guid claimId);

    Task<PayoutDto?> GetByIdAsync(Guid id, Guid? userId = null, Role? role = null);
    Task<PayoutDto?> GetByClaimIdAsync(Guid claimId, Guid? userId = null, Role? role = null);
    Task<PaginatedResult<PayoutDto>> GetHistoryAsync(PayoutHistoryQueryDto query);
    Task<PaginatedResult<PayoutDto>> GetMyPayoutsAsync(Guid policyholderId, PayoutHistoryQueryDto query);

    /// <summary>
    /// Update a draft payout proposal.
    /// </summary>
    Task<PayoutDto> UpdatePayoutAsync(Guid id);

    /// <summary>
    /// Approve a payout. Reviewer identity from server auth context.
    /// </summary>
    Task<PayoutDto> ApprovePayoutAsync(Guid id, string comments, Guid reviewerId, string reviewerName);

    /// <summary>
    /// Reject a payout. Reviewer identity from server auth context.
    /// </summary>
    Task<PayoutDto> RejectPayoutAsync(Guid id, string comments, Guid reviewerId, string reviewerName);

    /// <summary>
    /// Request revision on a payout. Reviewer identity from server auth context.
    /// </summary>
    Task<PayoutDto> RequestRevisionAsync(Guid id, string comments, Guid reviewerId, string reviewerName);

    /// <summary>
    /// Execute an approved payout via the payment gateway.
    /// Must have valid human approval before execution.
    /// </summary>
    Task<PayoutDto> ExecutePayoutAsync(Guid id);

    /// <summary>
    /// Delete a draft/invalid payout. Cannot delete approved/completed records.
    /// </summary>
    Task DeletePayoutAsync(Guid id);
}
