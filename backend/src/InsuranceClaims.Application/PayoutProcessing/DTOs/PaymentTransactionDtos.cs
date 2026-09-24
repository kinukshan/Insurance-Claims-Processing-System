using InsuranceClaims.Domain.PayoutProcessing;

namespace InsuranceClaims.Application.PayoutProcessing.DTOs;

/// <summary>
/// Staff-facing payment transaction detail DTO.
/// </summary>
public class PaymentTransactionDto
{
    public Guid Id { get; set; }
    public Guid PayoutId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentTransactionStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? ProviderBatchId { get; set; }
    public string? ProviderItemId { get; set; }
    public string? SenderBatchId { get; set; }
    public string? SenderItemId { get; set; }
    public string? Recipient { get; set; }
    public string? ProviderStatusRaw { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Policyholder-facing safe payment status DTO.
/// Does not expose internal provider errors or idempotency details.
/// </summary>
public class PaymentStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Webhook event payload for payment status updates.
/// </summary>
public class PaymentWebhookEventDto
{
    /// <summary>Provider event ID for replay safety.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Event type (e.g., "payment.processing", "payment.succeeded", "payment.failed").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Provider transaction ID.</summary>
    public string ProviderTransactionId { get; set; } = string.Empty;

    /// <summary>Provider-reported status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Payment amount for verification.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Currency code (e.g. "USD").</summary>
    public string? Currency { get; set; }

    /// <summary>Failure code if applicable.</summary>
    public string? FailureCode { get; set; }

    /// <summary>Failure message if applicable.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>Timestamp of the event.</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Response from payment execution endpoint.
/// </summary>
public class PaymentExecutionResultDto
{
    public Guid PayoutId { get; set; }
    public Guid? TransactionId { get; set; }
    public string? Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderBatchId { get; set; }
    public string? ProviderItemId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
