using Freitty.Application.Common;

namespace Freitty.Api.Reporting;

internal static class ReportingTimeZoneSetting
{
    private const string TimeZoneKey = "Cabinet:TimeZone";
    private const string TimeZoneVariable = "Cabinet__TimeZone";

    // Read while the services are registered, so a wrong zone stops the startup instead of skewing every date
    // filter. The id must be IANA, the rule of a hub's zone, because PostgreSQL is handed the same id.
    public static IServiceCollection AddReportingTimeZone(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? timeZoneId = configuration[TimeZoneKey];
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out TimeZoneInfo? zone)
            || !zone.HasIanaId)
        {
            throw new InvalidOperationException(
                $"The reporting time zone '{timeZoneId}' is not an IANA time zone id known to this system. " +
                $"Set the {TimeZoneVariable} environment variable to one, such as America/Toronto.");
        }

        services.AddSingleton(new ReportingTimeZone(zone));
        return services;
    }
}
