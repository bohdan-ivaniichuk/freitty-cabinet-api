using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EmptyDatabaseIsMigratedWhenApiStarts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string connectionString = await postgres.CreateDatabaseAsync(cancellationToken);
        await using var database = new TestDatabase(connectionString);

        (string[] Known, string[] Applied) migrations = await database.InContextAsync(async db =>
            (db.Database.GetMigrations().ToArray(),
                (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray()));

        Assert.NotEmpty(migrations.Known);
        Assert.Equal(migrations.Known, migrations.Applied);
    }

    [Fact]
    public async Task ModelHasNoChangesMissingFromMigrations()
    {
        await using var database = new TestDatabase(postgres.ConnectionString);

        bool hasPendingChanges = await database.InContextAsync(
            db => Task.FromResult(db.Database.HasPendingModelChanges()));

        Assert.False(hasPendingChanges);
    }

    [Fact]
    public async Task OrderListIndexesExistWithTheirMethods()
    {
        await using var database = new TestDatabase(postgres.ConnectionString);

        List<string> definitions = await database.InContextAsync(db => db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains(
            "CREATE INDEX ix_orders_number_trgm ON public.orders USING gin (number gin_trgm_ops)",
            definitions);
        Assert.Contains(
            "CREATE INDEX ix_shipments_ref_number_trgm ON public.shipments USING gin (ref_number gin_trgm_ops)",
            definitions);
        Assert.Contains(
            "CREATE INDEX ix_orders_company_id_scheduled_at_id ON public.orders USING btree "
                + "(company_id, scheduled_at DESC, id DESC)",
            definitions);
    }

    // Each start runs both migrations of an empty database; without the lock in DatabaseMigration two of them
    // would apply the second migration twice and one would fail.
    [Fact]
    public async Task InstancesStartingTogetherOnAnEmptyDatabaseApplyEachMigrationOnce()
    {
        const int InstanceCount = 4;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string connectionString = await postgres.CreateDatabaseAsync(cancellationToken);
        ApiFactory[] instances = Enumerable.Range(0, InstanceCount)
            .Select(_ => new ApiFactory(connectionString))
            .ToArray();

        try
        {
            await Task.WhenAll(instances.Select(instance =>
                Task.Run(() => instance.CreateClient().Dispose(), cancellationToken)));

            await using var database = new TestDatabase(connectionString);
            (string[] Known, List<string> Recorded) migrations = await database.InContextAsync(async db =>
                (db.Database.GetMigrations().ToArray(),
                    await db.Database
                        .SqlQuery<string>($"SELECT migration_id AS \"Value\" FROM __ef_migrations_history")
                        .ToListAsync(cancellationToken)));
            Assert.Equal(migrations.Known.Order(), migrations.Recorded.Order());
        }
        finally
        {
            foreach (ApiFactory instance in instances)
            {
                await instance.DisposeAsync();
            }
        }
    }
}
