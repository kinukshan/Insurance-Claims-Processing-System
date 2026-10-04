using InsuranceClaims.Application.Common.Interfaces;

namespace InsuranceClaims.Application.Common;

/// <summary>
/// Authoritative Sri Lanka business calendar implementation (Asia/Colombo, UTC+05:30).
/// Supports custom UTC clock delegate for deterministic testing.
/// </summary>
public class BusinessCalendar : IBusinessCalendar
{
    private static readonly Lazy<TimeZoneInfo> _sriLankaTimeZone = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        }
        catch (Exception)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
            }
            catch (Exception)
            {
                return TimeZoneInfo.CreateCustomTimeZone(
                    "Asia/Colombo",
                    TimeSpan.FromHours(5.5),
                    "Sri Lanka Standard Time",
                    "Sri Lanka Standard Time");
            }
        }
    });

    /// <summary>
    /// The resolved Sri Lanka TimeZoneInfo (Asia/Colombo / UTC+05:30).
    /// </summary>
    public static TimeZoneInfo SriLankaTimeZone => _sriLankaTimeZone.Value;

    private static IBusinessCalendar _default = new BusinessCalendar();

    /// <summary>
    /// Global default business calendar instance.
    /// </summary>
    public static IBusinessCalendar Default
    {
        get => _default;
        set => _default = value ?? new BusinessCalendar();
    }

    private readonly Func<DateTime> _utcNowFunc;

    public BusinessCalendar(Func<DateTime>? utcNowFunc = null)
    {
        _utcNowFunc = utcNowFunc ?? (() => DateTime.UtcNow);
    }

    /// <inheritdoc />
    public DateTime Today => ToBusinessDate(_utcNowFunc());

    /// <inheritdoc />
    public DateTime ToBusinessDate(DateTime utcInstant)
    {
        DateTime utc;
        if (utcInstant.Kind == DateTimeKind.Local)
        {
            utc = utcInstant.ToUniversalTime();
        }
        else if (utcInstant.Kind == DateTimeKind.Utc)
        {
            utc = utcInstant;
        }
        else
        {
            utc = DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
        }

        var businessTime = TimeZoneInfo.ConvertTimeFromUtc(utc, SriLankaTimeZone);
        return businessTime.Date;
    }
}
