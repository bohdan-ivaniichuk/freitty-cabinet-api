using Freitty.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence.Seed;

// The sample data of a demo that keeps itself fresh, with Seed:Refresh on: rebuilt at startup and again once the
// anchor day has moved on. One per process, so it remembers which anchor day the data it last built is dated from.
// The photos go with the data: the sample images are uploaded before the database is rebuilt, and the uploads
// deleted once the rebuilt data, which has no rows for them, is committed. A failure after the commit leaves at worst
// uploads that no row names, and the data still counts as outdated, so the next check rebuilds it again.
public sealed class SampleData(IServiceScopeFactory scopes, IPhotoStorage photoStorage, TimeProvider clock)
{
    private DateOnly? builtForAnchorDay;

    public bool IsOutdated => new SeedCalendar(clock, ReferenceData.HubTimeZone).AnchorDay != builtForAnchorDay;

    public async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await SamplePhotos.UploadAsync(photoStorage, cancellationToken);
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        DateOnly anchorDay = await seeder.RebuildAsync(cancellationToken);
        await photoStorage.DeleteUploadsAsync(cancellationToken);
        builtForAnchorDay = anchorDay;
    }
}
