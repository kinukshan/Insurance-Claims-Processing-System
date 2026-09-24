using InsuranceClaims.Application.PayoutProcessing.Interfaces;
using InsuranceClaims.Domain.PayoutProcessing;
using InsuranceClaims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InsuranceClaims.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IPaymentTransactionRepository.
/// </summary>
public class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentTransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentTransaction?> GetByIdAsync(Guid id)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
    }

    public async Task<PaymentTransaction?> GetByProviderTransactionIdAsync(string providerTransactionId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == providerTransactionId);
    }

    public async Task<List<PaymentTransaction>> GetByPayoutIdAsync(Guid payoutId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.PayoutId == payoutId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<PaymentTransaction?> GetActiveByPayoutIdAsync(Guid payoutId)
    {
        return await _context.PaymentTransactions
            .Where(t => t.PayoutId == payoutId
                && (t.Status == PaymentTransactionStatus.Created
                    || t.Status == PaymentTransactionStatus.Processing))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<PaymentTransaction> AddAsync(PaymentTransaction transaction)
    {
        _context.PaymentTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task UpdateAsync(PaymentTransaction transaction)
    {
        var entry = _context.Entry(transaction);
        if (entry.State == EntityState.Detached)
        {
            _context.PaymentTransactions.Attach(transaction);
            entry.State = EntityState.Modified;
        }
        else if (entry.State == EntityState.Unchanged)
        {
            entry.State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasSucceededTransactionAsync(Guid payoutId)
    {
        return await _context.PaymentTransactions
            .AnyAsync(t => t.PayoutId == payoutId
                && t.Status == PaymentTransactionStatus.Succeeded);
    }

    public async Task<PaymentTransaction?> GetByProviderBatchIdAsync(string providerBatchId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderBatchId == providerBatchId);
    }

    public async Task<PaymentTransaction?> GetByProviderItemIdAsync(string providerItemId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.ProviderItemId == providerItemId);
    }

    public async Task<PaymentTransaction?> GetBySenderBatchIdAsync(string senderBatchId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.SenderBatchId == senderBatchId);
    }

    public async Task<PaymentTransaction?> GetBySenderItemIdAsync(string senderItemId)
    {
        return await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.SenderItemId == senderItemId);
    }

    public async Task<bool> HasWebhookEventBeenProcessedAsync(string provider, string providerEventId)
    {
        return await _context.PaymentWebhookEvents
            .AnyAsync(e => e.Provider == provider && e.ProviderEventId == providerEventId);
    }

    public async Task<PaymentWebhookEvent> AddWebhookEventAsync(PaymentWebhookEvent webhookEvent)
    {
        _context.PaymentWebhookEvents.Add(webhookEvent);
        await _context.SaveChangesAsync();
        return webhookEvent;
    }

    public async Task<List<PaymentWebhookEvent>> GetWebhookEventsByTransactionIdAsync(Guid transactionId)
    {
        return await _context.PaymentWebhookEvents
            .Where(e => e.PaymentTransactionId == transactionId)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();
    }
}
