namespace Freitty.Api.IntegrationTests.Dashboard;

// An instant written as the wall clock in Toronto, the reporting zone, on either side of a daylight saving change.
internal static class Toronto
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");

    public static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0)
    {
        var wallClock = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallClock, Zone.GetUtcOffset(wallClock)).ToUniversalTime();
    }
}
