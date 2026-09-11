using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IPayoutRepository.
/// </summary>
public class PayoutRepository : IPayoutRepository
{
    private readonly Persistence.ApplicationDbContext _context;

    public PayoutRepository(Persistence.ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Payout?> GetByIdAsync(Guid id)
    {
        return await _context.Payouts
            .Include(p => p.Approvals)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Payout?> GetByClaimIdAsync(Guid claimId)
    {
        return await _context.Payouts
            .Include(p => p.Approvals)
            .FirstOrDefaultAsync(p => p.ClaimId == claimId);
    }

    public async Task<List<Payout>> GetAllAsync()
    {
        return await _context.Payouts
            .Include(p => p.Approvals)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<Payout> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, PayoutStatus? statusFilter, string? sortBy, bool sortDescending)
    {
        var query = _context.Payouts
            .Include(p => p.Approvals)
            .AsQueryable();

        // Filter
        if (statusFilter.HasValue)
        {
            query = query.Where(p => p.Status == statusFilter.Value);
        }

        // Sort
        query = sortBy?.ToLowerInvariant() switch
        {
            "amount" or "finalpayout" => sortDescending
                ? query.OrderByDescending(p => p.FinalPayout)
                : query.OrderBy(p => p.FinalPayout),
            "status" => sortDescending
                ? query.OrderByDescending(p => p.Status)
                : query.OrderBy(p => p.Status),
            _ => sortDescending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Payout> AddAsync(Payout payout)
    {
        _context.Payouts.Add(payout);
        await _context.SaveChangesAsync();
        return payout;
    }

    public async Task UpdateAsync(Payout payout)
    {
        _context.Payouts.Update(payout);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Payout payout)
    {
        _context.Payouts.Remove(payout);
        await _context.SaveChangesAsync();
    }
}
