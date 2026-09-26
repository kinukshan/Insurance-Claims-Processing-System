using InsuranceClaims.Application.Notifications.DTOs;

namespace InsuranceClaims.Application.Notifications.Interfaces;

/// <summary>
/// Provider-independent email delivery abstraction.
///
/// Implementations:
///   - MockEmailService   (Phase 1 — no network, no real emails)
///   - SendGridEmailService (future)
///   - SesEmailService      (future)
///
/// Contract:
///   - MUST NOT throw on delivery failure — return EmailSendResult with Success=false.
///   - MUST NOT make outbound network calls in Mock mode.
///   - MUST be safe to call in a fire-and-forget pattern.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Attempt to send an email message.
    /// Returns a result object; never throws on delivery failure.
    /// </summary>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
