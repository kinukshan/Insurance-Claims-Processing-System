namespace InsuranceClaims.Application.Notifications.DTOs;

/// <summary>
/// Provider-agnostic email message passed to IEmailService implementations.
/// </summary>
public record EmailMessage
{
    /// <summary>Recipient email address.</summary>
    public required string To { get; init; }

    /// <summary>Email subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML or plain-text body.</summary>
    public required string Body { get; init; }

    /// <summary>
    /// Optional idempotency key to prevent duplicate sends.
    /// Format: "{UserId}:{ClaimId}:{NotificationType}"
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Result returned by IEmailService after attempting delivery.
/// </summary>
public record EmailSendResult
{
    /// <summary>Whether the send attempt succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Provider name (e.g., "Mock", "SendGrid", "SES").</summary>
    public required string Provider { get; init; }

    /// <summary>Provider-specific message ID for traceability.</summary>
    public string? MessageId { get; init; }

    /// <summary>Error description on failure; null on success.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO for NotificationLog API responses.
/// </summary>
public record NotificationLogDto(
    Guid Id,
    Guid UserId,
    Guid? ClaimId,
    string NotificationType,
    string Channel,
    string Recipient,
    string Subject,
    bool Success,
    string? ErrorMessage,
    string Provider,
    string? ProviderMessageId,
    DateTime? SentAt,
    DateTime CreatedAt,
    Guid? PayoutId = null,
    string? NotificationKey = null,
    string? Status = null
);
