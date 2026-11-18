using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Freitty.Api.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres)
{
    private const string SequenceMigrationSuffix = "_OrderNumberSequence";
    private const string MigrationsHistoryTable = "__ef_migrations_history";
    private const string StoredOfficialNumber = "FR004321";
    private const long NumberAfterTheStoredOne = 4322;
    [Fact]
    public async Task EmptyDatabaseIsMigratedWhenApiStarts()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using var database = new TestDatabase(connectionString);

        (string[] Known, string[] Applied) migrations = await database.InContextAsync(async db =>
            (db.Database.GetMigrations().ToArray(),
                (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToArray()));

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
        Assert.Contains(
            "CREATE INDEX ix_orders_company_id_status_scheduled_at_id ON public.orders USING btree "
                + "(company_id, status, scheduled_at DESC, id DESC)",
            definitions);
    }

    // Each start runs every migration of an empty database; without the lock in DatabaseMigration two instances
    // would apply the same migration twice and one of them would fail.
    [Fact]
    public async Task InstancesStartingTogetherOnAnEmptyDatabaseApplyEachMigrationOnce()
    {
        const int InstanceCount = 4;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string connectionString = await postgres.CreateDatabaseAsync();
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

    // Every other database migrates while empty. This one stops before the sequence, gets a submitted order the
    // way a database already in use would have one, and then takes the rest of the migrations.
    [Fact]
    public async Task SequenceMigrationContinuesAfterTheOfficialNumbersAlreadyStored()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AppDbContext db = ContextOn(await postgres.CreateDatabaseAsync());
        IMigrator migrator = db.GetService<IMigrator>();
        string[] migrations = db.Database.GetMigrations().ToArray();
        int sequenceIndex = Array.FindIndex(
            migrations,
            name => name.EndsWith(SequenceMigrationSuffix, StringComparison.Ordinal));
        await migrator.MigrateAsync(migrations[sequenceIndex - 1], cancellationToken: cancellationToken);

        TestData data = TestData.Create();
        Order order = new OrderBuilder(data).SubmittableDraft();
        order.Submit(StoredOfficialNumber, OrderBuilder.BuiltAt).MustSucceed();
        db.AddRange([.. data.Entities(), order]);
        await db.SaveChangesAsync(cancellationToken);

        await migrator.MigrateAsync(cancellationToken: cancellationToken);
        long next = await db.Database
            .SqlQuery<long>($"SELECT nextval('order_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        Assert.Equal(NumberAfterTheStoredOne, next);
    }

    // As the API configures it, without starting the API, which would apply every migration at once.
    private static AppDbContext ContextOn(string connectionString)
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
