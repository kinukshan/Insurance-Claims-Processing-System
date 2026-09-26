using InsuranceClaims.Application.Notifications.Interfaces;
using InsuranceClaims.Domain.Notifications;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the notification log repository.
/// </summary>
public class NotificationLogRepository : INotificationLogRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<NotificationLog> AddAsync(NotificationLog log)
    {
        try
        {
            var existing = await _context.NotificationLogs
                .FirstOrDefaultAsync(n => n.NotificationKey == log.NotificationKey);

            if (existing != null)
            {
                // If previous attempt failed and current attempt succeeded, update existing record (preserving CreatedAt)
                if (!existing.Success && log.Success)
                {
                    existing.Status = NotificationStatus.Sent;
                    existing.Success = true;
                    existing.SentAt = log.SentAt ?? DateTime.UtcNow;
                    existing.Provider = log.Provider;
                    existing.ProviderMessageId = log.ProviderMessageId;
                    existing.ErrorMessage = null;
                    existing.Subject = log.Subject;
                    existing.Body = log.Body;
                    existing.Recipient = log.Recipient;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                return existing;
            }

            if (log.Status == NotificationStatus.Pending)
            {
                log.Status = log.Success ? NotificationStatus.Sent : NotificationStatus.Failed;
            }

            _context.NotificationLogs.Add(log);
            await _context.SaveChangesAsync();
            return log;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _context.Entry(log).State = EntityState.Detached;

            var existing = await _context.NotificationLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.NotificationKey == log.NotificationKey);

            if (existing != null)
            {
                return existing;
            }

            throw new DuplicateNotificationException(
                $"A notification with key '{log.NotificationKey}' has already been processed.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<NotificationReservationResult> TryReserveAsync(NotificationLog draftLog, TimeSpan? leaseDuration = null)
    {
        var effectiveLease = leaseDuration ?? TimeSpan.FromMinutes(5);
        try
        {
            var existing = await _context.NotificationLogs
                .FirstOrDefaultAsync(n => n.NotificationKey == draftLog.NotificationKey);

            if (existing != null)
            {
                // If already delivered / sent / accepted successfully
                if (existing.Status == NotificationStatus.Sent || existing.Status == NotificationStatus.Accepted || existing.Success)
                {
                    return new NotificationReservationResult(
                        IsReserved: false,
                        AlreadyDelivered: true,
                        Log: existing);
                }

                // If currently processing, check lease
                if (existing.Status == NotificationStatus.Processing)
                {
                    var isLeaseExpired = DateTime.UtcNow - existing.UpdatedAt > effectiveLease;
                    if (!isLeaseExpired)
                    {
                        // Active lease owned by another concurrent operation
                        return new NotificationReservationResult(
                            IsReserved: false,
                            AlreadyDelivered: false,
                            Log: existing);
                    }

                    // Lease expired — crash or timeout recovery: reclaim lease
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Provider = draftLog.Provider;
                    existing.Recipient = draftLog.Recipient;
                    existing.Subject = draftLog.Subject;
                    existing.Body = draftLog.Body;
                    existing.ErrorMessage = null;
                    await _context.SaveChangesAsync();

                    return new NotificationReservationResult(
                        IsReserved: true,
                        AlreadyDelivered: false,
                        Log: existing);
                }

                // If failed previously or pending: reclaim for retry (preserve original CreatedAt)
                existing.Status = NotificationStatus.Processing;
                existing.Success = false;
                existing.SentAt = null;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.ErrorMessage = null;
                existing.Provider = draftLog.Provider;
                existing.Recipient = draftLog.Recipient;
                existing.Subject = draftLog.Subject;
                existing.Body = draftLog.Body;

                await _context.SaveChangesAsync();

                return new NotificationReservationResult(
                    IsReserved: true,
                    AlreadyDelivered: false,
                    Log: existing);
            }

            // No existing record — insert new reservation
            draftLog.Status = NotificationStatus.Processing;
            draftLog.Success = false;
            draftLog.SentAt = null;
            draftLog.CreatedAt = DateTime.UtcNow;
            draftLog.UpdatedAt = DateTime.UtcNow;

            _context.NotificationLogs.Add(draftLog);
            await _context.SaveChangesAsync();

            return new NotificationReservationResult(
                IsReserved: true,
                AlreadyDelivered: false,
                Log: draftLog);
        }
        catch (DbUpdateException ex)
        {
            try
            {
                _context.Entry(draftLog).State = EntityState.Detached;
            }
            catch { /* ignore if already detached */ }

            var winner = await _context.NotificationLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.NotificationKey == draftLog.NotificationKey);

            if (winner != null)
            {
                var isDelivered = winner.Status == NotificationStatus.Sent || winner.Status == NotificationStatus.Accepted || winner.Success;
                return new NotificationReservationResult(
                    IsReserved: false,
                    AlreadyDelivered: isDelivered,
                    Log: winner);
            }

            if (IsUniqueConstraintViolation(ex))
            {
                return new NotificationReservationResult(
                    IsReserved: false,
                    AlreadyDelivered: false,
                    Log: null);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<NotificationLog> MarkSentAsync(string notificationKey, string provider, string? providerMessageId)
    {
        var log = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        if (log == null)
        {
            throw new InvalidOperationException($"Notification log with key '{notificationKey}' not found.");
        }

        log.Status = NotificationStatus.Sent;
        log.Success = true;
        log.SentAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        log.Provider = provider;
        log.ProviderMessageId = providerMessageId;
        log.ErrorMessage = null;

        await _context.SaveChangesAsync();
        return log;
    }

    /// <inheritdoc />
    public async Task<NotificationLog> MarkAcceptedAsync(string notificationKey, string provider, string? providerMessageId)
    {
        var log = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        if (log == null)
        {
            throw new InvalidOperationException($"Notification log with key '{notificationKey}' not found.");
        }

        log.Status = NotificationStatus.Accepted;
        log.Success = true;
        log.SentAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        log.Provider = provider;
        log.ProviderMessageId = providerMessageId;
        log.ErrorMessage = null;

        await _context.SaveChangesAsync();
        return log;
    }

    /// <inheritdoc />
    public async Task<NotificationLog> MarkFailedAsync(string notificationKey, string provider, string? errorMessage)
    {
        var log = await _context.NotificationLogs
            .FirstOrDefaultAsync(n => n.NotificationKey == notificationKey);

        if (log == null)
        {
            throw new InvalidOperationException($"Notification log with key '{notificationKey}' not found.");
        }

        log.Status = NotificationStatus.Failed;
        log.Success = false;
        log.SentAt = null;
        log.UpdatedAt = DateTime.UtcNow;
        log.Provider = provider;
        log.ErrorMessage = errorMessage != null && errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;

        await _context.SaveChangesAsync();
        return log;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
            return true;

        var innerType = ex.InnerException?.GetType().FullName ?? string.Empty;
        if (innerType.Contains("SqliteException", StringComparison.OrdinalIgnoreCase))
        {
            var msgSqlite = ex.InnerException?.Message ?? string.Empty;
            if (msgSqlite.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                msgSqlite.Contains("19", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("23505", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("IX_NotificationLogs_NotificationKey", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<List<NotificationLog>> GetByUserIdAsync(Guid userId)
    {
        return await _context.NotificationLogs
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<NotificationLog>> GetByClaimIdAsync(Guid claimId)
    {
        return await _context.NotificationLogs
            .Where(n => n.ClaimId == claimId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<NotificationLog>> GetByPayoutIdAsync(Guid payoutId)
    {
        return await _context.NotificationLogs
            .Where(n => n.PayoutId == payoutId)
            .OrderByDescending(n => n.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByKeyAsync(string notificationKey)
    {
        if (string.IsNullOrWhiteSpace(notificationKey))
            return false;

        return await _context.NotificationLogs
            .AnyAsync(n => n.NotificationKey == notificationKey && (n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Accepted || n.Success));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid userId, Guid? claimId, NotificationType type)
    {
        return await _context.NotificationLogs
            .AnyAsync(n =>
                n.UserId == userId &&
                n.ClaimId == claimId &&
                n.NotificationType == type &&
                (n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Accepted || n.Success));
    }

    /// <inheritdoc />
    public async Task<List<NotificationLog>> GetAllAsync(int page = 1, int pageSize = 50)
    {
        return await _context.NotificationLogs
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }
}
