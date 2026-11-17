using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence;

public static class DatabaseMigration
{
    // Runs before the API listens. A failure is not caught: an instance that cannot bring its schema up to date
    // must not serve requests. Instances starting together do not race; EF Core locks the database meanwhile.
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
