using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Repository interface for Payout persistence.
/// </summary>
public interface IPayoutRepository
{
    Task<Payout?> GetByIdAsync(Guid id);
    Task<Payout?> GetByClaimIdAsync(Guid claimId);
    Task<List<Payout>> GetAllAsync();
    Task<(List<Payout> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, PayoutStatus? statusFilter, string? sortBy, bool sortDescending);
    Task<Payout> AddAsync(Payout payout);
    Task UpdateAsync(Payout payout);
    Task DeleteAsync(Payout payout);
}
