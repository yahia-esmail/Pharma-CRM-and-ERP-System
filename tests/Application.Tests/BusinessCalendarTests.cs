using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PharmaERP.Application.Common;

namespace PharmaERP.Application.Tests;

internal static class TestCalendar
{
    /// <summary>The production default (Africa/Cairo), on the given clock.</summary>
    public static BusinessCalendar Cairo(TimeProvider? time = null) =>
        new(Options.Create(new BusinessOptions()), time ?? TimeProvider.System);
}

public class BusinessCalendarTests
{
    private static BusinessCalendar At(int year, int month, int day, int hour, int minute = 0) =>
        TestCalendar.Cairo(new FakeTimeProvider(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero)));

    [Fact]
    public void After_local_midnight_it_is_already_tomorrow_while_utc_is_still_yesterday()
    {
        // 23:00 UTC on 1 Oct = 02:00 on 2 Oct in Cairo (summer time, UTC+3).
        Assert.Equal(new DateOnly(2026, 10, 2), At(2026, 10, 1, 23).Today);
        Assert.Equal(new DateOnly(2026, 10, 1), At(2026, 10, 1, 20, 59).Today);
    }

    [Fact]
    public void Summer_day_starts_at_21_utc_the_evening_before()
    {
        var calendar = TestCalendar.Cairo();
        Assert.Equal(new DateTime(2026, 10, 1, 21, 0, 0, DateTimeKind.Utc), calendar.StartOfDayUtc(new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public void Winter_day_starts_at_22_utc_the_evening_before()
    {
        var calendar = TestCalendar.Cairo();
        Assert.Equal(new DateTime(2026, 12, 14, 22, 0, 0, DateTimeKind.Utc), calendar.StartOfDayUtc(new DateOnly(2026, 12, 15)));
        Assert.Equal(new DateOnly(2026, 12, 15), calendar.DateOf(new DateTime(2026, 12, 14, 22, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Every_instant_of_a_day_maps_back_to_that_day_across_the_daylight_saving_changes()
    {
        var calendar = TestCalendar.Cairo();
        for (var date = new DateOnly(2026, 4, 20); date <= new DateOnly(2026, 11, 5); date = date.AddDays(1))
        {
            var start = calendar.StartOfDayUtc(date);
            var next = calendar.StartOfDayUtc(date.AddDays(1));
            Assert.Equal(date, calendar.DateOf(start));
            Assert.Equal(date, calendar.DateOf(next.AddTicks(-1)));
            Assert.InRange((next - start).TotalHours, 23, 25);
        }
    }

    [Fact]
    public void Unknown_time_zone_fails_at_startup_rather_than_silently_using_utc() =>
        Assert.ThrowsAny<TimeZoneNotFoundException>(() =>
            new BusinessCalendar(Options.Create(new BusinessOptions { TimeZone = "Mars/Olympus" }), TimeProvider.System));
}
