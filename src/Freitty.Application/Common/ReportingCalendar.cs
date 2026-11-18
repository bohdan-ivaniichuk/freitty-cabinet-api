namespace Freitty.Application.Common;

// Turns a date preset into UTC instants: the days are cut in the reporting zone, the result is compared with UTC
// columns. Weeks start on Monday.
public sealed class ReportingCalendar(TimeProvider clock, ReportingTimeZone reportingTimeZone)
{
    public const int DaysInLast30Days = 30;

    private const int DaysInWeek = 7;

    public InstantRange RangeOf(DatePreset preset)
    {
        DateOnly today = Today();
        DateOnly tomorrow = today.AddDays(1);
        return preset switch
        {
            DatePreset.Today => Between(today, tomorrow),
            DatePreset.ThisWeek => Between(MondayOf(today), MondayOf(today).AddDays(DaysInWeek)),
            DatePreset.Last30Days => Between(tomorrow.AddDays(-DaysInLast30Days), tomorrow),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "The date preset is not known."),
        };
    }

    private TimeZoneInfo Zone => reportingTimeZone.Zone;

    private static DateOnly MondayOf(DateOnly day)
    {
        int daysSinceMonday = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + DaysInWeek) % DaysInWeek;
        return day.AddDays(-daysSinceMonday);
    }

    private DateOnly Today()
    {
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private InstantRange Between(DateOnly firstDay, DateOnly dayAfterLast)
    {
        return new InstantRange(StartOf(firstDay), StartOf(dayAfterLast));
    }

    // A zone that changes its clocks at midnight has a day that starts at 01:00 or twice: a skipped midnight takes
    // the offset before the change, which lands on the first instant of the day; a repeated one takes the earlier
    // of its two instants.
    private DateTimeOffset StartOf(DateOnly day)
    {
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        TimeSpan offset = Zone.IsAmbiguousTime(midnight)
            ? Zone.GetAmbiguousTimeOffsets(midnight).Max()
            : Zone.GetUtcOffset(midnight);
        return new DateTimeOffset(midnight, offset).ToUniversalTime();
    }
}
