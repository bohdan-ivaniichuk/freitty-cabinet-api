using Microsoft.Extensions.Configuration;

namespace Freitty.Infrastructure.Persistence.Seed;

// The two switches of the sample data. Both are off unless the environment turns them on: absent is false, and a
// value that is not a boolean stops the startup instead of being read as either. Refresh has an effect only when
// the seed is enabled.
public sealed record SeedSettings(bool IsEnabled, bool IsRefreshEnabled)
{
    private const string EnabledKey = "Seed:Enabled";
    private const string RefreshKey = "Seed:Refresh";

    public static SeedSettings Read(IConfiguration configuration)
    {
        bool isEnabled = configuration.GetValue<bool>(EnabledKey);
        bool isRefreshRequested = configuration.GetValue<bool>(RefreshKey);
        return new SeedSettings(isEnabled, isEnabled && isRefreshRequested);
    }
}
