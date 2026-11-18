using Freitty.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence.Seed;

public static class DatabaseSeed
{
    // Runs after the migrations and before the API listens. With the seed enabled an empty database is filled; with
    // refresh on as well, the sample data is rebuilt whatever the database holds.
    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        SeedSettings settings = SeedSettings.Read(scope.ServiceProvider.GetRequiredService<IConfiguration>());
        if (!settings.IsEnabled)
        {
            return;
        }

        if (settings.IsRefreshEnabled)
        {
            await scope.ServiceProvider.GetRequiredService<SampleData>().RebuildAsync(cancellationToken);
            return;
        }

        // The images first: a seeded photo is a row that names one. A database that is kept keeps its uploads.
        IPhotoStorage photoStorage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();
        await SamplePhotos.UploadAsync(photoStorage, cancellationToken);
        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
