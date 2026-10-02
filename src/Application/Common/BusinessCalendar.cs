using Microsoft.Extensions.Options;

namespace PharmaERP.Application.Common;

public class BusinessOptions
{
    public const string SectionName = "Business";

    /// <summary>IANA zone the working day is counted in. Cairo observes daylight saving (UTC+2 / UTC+3),
    /// so a fixed offset would be wrong half the year.</summary>
    public string TimeZone { get; set; } = "Africa/Cairo";
}

/// <summary>Which calendar day an instant belongs to, in the business's time zone. Timestamps are stored in UTC,
/// but "today's plan", "visits today" and similar are about the rep's local working day: with plain UTC dates,
/// Cairo's 00:00–03:00 would still count as yesterday.</summary>
public interface IBusinessCalendar
{
    TimeZoneInfo TimeZone { get; }

    /// <summary>Today's date where the business operates.</summary>
    DateOnly Today { get; }

    /// <summary>The local business date of a UTC instant.</summary>
    DateOnly DateOf(DateTime utc);

    /// <summary>UTC instant at which <paramref name="date"/> starts locally. A day is
    /// [StartOfDayUtc(d), StartOfDayUtc(d + 1)) — use half-open ranges, never 23:59:59.</summary>
    DateTime StartOfDayUtc(DateOnly date);
}

public class BusinessCalendar(IOptions<BusinessOptions> options, TimeProvider time) : IBusinessCalendar
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOf(time.GetUtcNow().UtcDateTime);

    public DateOnly DateOf(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone));

    public DateTime StartOfDayUtc(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // On a spring-forward day midnight itself may not exist locally; the day then starts at the first valid minute.
        while (TimeZone.IsInvalidTime(localMidnight)) localMidnight = localMidnight.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, TimeZone);
    }
}
