namespace Freitty.Infrastructure.Persistence.Seed;

// The anchor day is the day before seeding, so every event placed on it or earlier is in the past whatever the
// hour: the aggregate refuses a change dated before the last one, and an event later today would block the first
// real write. All times used are daytime, clear of the night hours that daylight saving skips or repeats.
internal sealed class SeedCalendar
{
    private readonly TimeZoneInfo timeZone;

    public SeedCalendar(TimeProvider clock, string timeZoneId)
    {
        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone);
        AnchorDay = DateOnly.FromDateTime(localNow.DateTime).AddDays(-1);
    }

    public DateOnly AnchorDay { get; }

    public DateTimeOffset At(int dayOffset, int hour, int minute)
    {
        return At(dayOffset, new TimeOnly(hour, minute));
    }

    public DateTimeOffset At(int dayOffset, TimeOnly localTime)
    {
        DateTime localDateTime = AnchorDay.AddDays(dayOffset).ToDateTime(localTime);
        return new DateTimeOffset(localDateTime, timeZone.GetUtcOffset(localDateTime));
    }
}
