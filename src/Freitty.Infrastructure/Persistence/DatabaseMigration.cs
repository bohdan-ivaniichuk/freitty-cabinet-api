using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Persistence;

public static class DatabaseMigration
{
    // Any constant shared by every instance; it names the lock, nothing else uses it.
    private const long MigrationLockKey = 0x46524549_54545901;

    // The command timeout of the whole migration, not only of the wait for the lock: an instance queued behind
    // another one's migrations waits this long, and every migration command may run as long.
    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromMinutes(5);

    // Runs before the API listens. A failure is not caught: an instance that cannot bring its schema up to date
    // must not serve requests. The database itself must exist — Compose and the deployment create it.
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Instances starting together take turns. EF Core's own lock is not enough: Npgsql releases it with each
        // migration's transaction, while the list of pending migrations is read once, so two instances behind by
        // two migrations would both apply the second. A session lock on the context's connection spans them all.
        db.Database.SetCommandTimeout(MigrationTimeout);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            finally
            {
                await ReleaseLockAsync(db);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    // A pooled connection would keep its session lock until it is reused, so the lock is released explicitly. If
    // that fails, the connection is broken and the server has dropped the lock with the session; the failure is not
    // allowed to replace an exception from the migration itself.
    private static async Task ReleaseLockAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None);
        }
        catch (DbException)
        {
        }
    }
}
