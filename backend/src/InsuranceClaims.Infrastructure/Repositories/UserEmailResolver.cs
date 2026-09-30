using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// Resolves user email addresses from the Users table.
/// </summary>
public class UserEmailResolver : IUserEmailResolver
{
    private readonly ApplicationDbContext _context;

    public UserEmailResolver(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string?> GetEmailAsync(Guid userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        return user;
    }
}
