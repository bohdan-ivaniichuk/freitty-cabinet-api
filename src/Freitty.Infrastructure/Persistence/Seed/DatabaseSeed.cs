using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence.Seed;

public static class DatabaseSeed
{
    private const string EnabledKey = "Seed:Enabled";

    // Runs after the migrations and before the API listens. Off unless the environment turns it on: absent is
    // false, and a value that is not a boolean stops the startup instead of being read as either.
    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>(EnabledKey))
        {
            return;
        }

        DatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
