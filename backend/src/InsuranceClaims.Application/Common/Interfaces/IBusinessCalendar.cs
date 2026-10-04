namespace InsuranceClaims.Application.Common.Interfaces;

/// <summary>
/// Authoritative business calendar source for date-only business rules.
/// Resolves the business "today" in Sri Lanka standard time (Asia/Colombo, UTC+05:30)
/// from a UTC instant.
/// </summary>
public interface IBusinessCalendar
{
    /// <summary>
    /// Gets today's calendar date in the business timezone.
    /// </summary>
    DateTime Today { get; }

    /// <summary>
    /// Converts a UTC instant to the calendar date in the business timezone.
    /// </summary>
    DateTime ToBusinessDate(DateTime utcInstant);
}
