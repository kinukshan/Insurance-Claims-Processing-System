using InsuranceClaims.Application.ClaimsManagement.Interfaces;
using InsuranceClaims.Domain.ClaimsManagement;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the claim repository.
/// </summary>
public class ClaimRepository : IClaimRepository
{
    private readonly ApplicationDbContext _context;

    public ClaimRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Claim?> GetByIdAsync(Guid id)
    {
        return await _context.Claims.FindAsync(id);
    }

    public async Task<Claim?> GetByIdWithDocumentsAsync(Guid id)
    {
        return await _context.Claims
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Claim>> GetByPolicyHolderIdAsync(Guid policyHolderId)
    {
        return await _context.Claims
            .Where(c => c.PolicyHolderId == policyHolderId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Claim>> GetAllAsync(string? statusFilter = null, string? searchTerm = null, Guid? policyHolderId = null)
    {
        var query = _context.Claims.AsQueryable();

        if (policyHolderId.HasValue)
        {
            query = query.Where(c => c.PolicyHolderId == policyHolderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<ClaimStatus>(statusFilter, true, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(c =>
                c.ClaimNumber.ToLower().Contains(term) ||
                c.Description.ToLower().Contains(term) ||
                c.IncidentLocation.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Claim> AddAsync(Claim claim)
    {
        _context.Claims.Add(claim);
        await _context.SaveChangesAsync();
        return claim;
    }

    public async Task<Claim> UpdateAsync(Claim claim)
    {
        var entry = _context.Entry(claim);
        if (entry.State == EntityState.Detached)
        {
            _context.Claims.Attach(claim);
            entry.State = EntityState.Modified;
        }

        if (claim.Documents != null)
        {
            foreach (var doc in claim.Documents)
            {
                var docEntry = _context.Entry(doc);
                if (docEntry.State == EntityState.Detached)
                {
                    _context.ClaimDocuments.Add(doc);
                }
                else if (docEntry.State == EntityState.Modified)
                {
                    var exists = await _context.ClaimDocuments.AnyAsync(d => d.Id == doc.Id);
                    if (!exists)
                    {
                        docEntry.State = EntityState.Added;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        return claim;
    }

    public async Task DeleteAsync(Claim claim)
    {
        _context.Claims.Remove(claim);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteDocumentAsync(ClaimDocument document)
    {
        _context.ClaimDocuments.Remove(document);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Claims.AnyAsync(c => c.Id == id);
    }

    public async Task<string> GenerateClaimNumberAsync()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _context.Claims
            .CountAsync(c => c.CreatedAt.Date == DateTime.UtcNow.Date);
        return $"CLM-{date}-{(count + 1):D4}";
    }
}
