using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence.Seed;

// The sample data of a demo that keeps itself fresh, with Seed:Refresh on: rebuilt at startup and again once the
// anchor day has moved on. One per process, so it remembers which anchor day the data it last built is dated from.
public sealed class SampleData(IServiceScopeFactory scopes, TimeProvider clock)
{
    private DateOnly? builtForAnchorDay;

    public bool IsOutdated => new SeedCalendar(clock, ReferenceData.HubTimeZone).AnchorDay != builtForAnchorDay;

    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        builtForAnchorDay = await seeder.RebuildAsync(cancellationToken);
    }
}
