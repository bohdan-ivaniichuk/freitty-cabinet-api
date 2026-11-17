using Freitty.Domain.Common;

namespace Freitty.Domain.Hubs;

public sealed class Hub
{
    public Hub(string name, string shortName, string region, string timeZone)
    {
        string timeZoneId = IanaTimeZone(timeZone);
        Id = Guid.CreateVersion7();
        Name = Guard.Text(name);
        ShortName = Guard.Text(shortName);
        Region = Guard.Text(region);
        TimeZone = timeZoneId;
    }

    private Hub()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string ShortName { get; private set; } = null!;

    public string Region { get; private set; } = null!;

    public string TimeZone { get; private set; } = null!;

    // PostgreSQL knows IANA ids only; a Windows id would fail every query that groups by hub-local time.
    private static string IanaTimeZone(string timeZone)
    {
        string timeZoneId = Guard.Text(timeZone);
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out TimeZoneInfo? timeZoneInfo) || !timeZoneInfo.HasIanaId)
        {
            throw new ArgumentException(
                $"'{timeZoneId}' is not an IANA time zone id known to this system, such as America/Toronto.",
                nameof(timeZone));
        }

        return timeZoneId;
    }
}
