using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.Interfaces;

/// <summary>
/// Repository interface for PaymentTransaction persistence.
/// </summary>
public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByIdAsync(Guid id);
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<PaymentTransaction?> GetByProviderTransactionIdAsync(string providerTransactionId);
    Task<PaymentTransaction?> GetByProviderBatchIdAsync(string providerBatchId);
    Task<PaymentTransaction?> GetByProviderItemIdAsync(string providerItemId);
    Task<PaymentTransaction?> GetBySenderBatchIdAsync(string senderBatchId);
    Task<PaymentTransaction?> GetBySenderItemIdAsync(string senderItemId);
    Task<List<PaymentTransaction>> GetByPayoutIdAsync(Guid payoutId);
    Task<PaymentTransaction?> GetActiveByPayoutIdAsync(Guid payoutId);
    Task<PaymentTransaction> AddAsync(PaymentTransaction transaction);
    Task UpdateAsync(PaymentTransaction transaction);
    Task<bool> HasSucceededTransactionAsync(Guid payoutId);

    // Webhook Event persistence
    Task<bool> HasWebhookEventBeenProcessedAsync(string provider, string providerEventId);
    Task<PaymentWebhookEvent> AddWebhookEventAsync(PaymentWebhookEvent webhookEvent);
    Task<List<PaymentWebhookEvent>> GetWebhookEventsByTransactionIdAsync(Guid transactionId);
}
