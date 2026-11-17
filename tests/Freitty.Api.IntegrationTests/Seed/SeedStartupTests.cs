using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Seed;

public sealed class SeedStartupTests(PostgresFixture postgres)
{
    private const string LatestInstantSql = """
        SELECT max(instant) AS "Value" FROM (
            SELECT created_at AS instant FROM orders
            UNION ALL SELECT submitted_at FROM orders
            UNION ALL SELECT started_at FROM orders
            UNION ALL SELECT completed_at FROM orders
            UNION ALL SELECT updated_at FROM orders
            UNION ALL SELECT arrived_at FROM shipments
            UNION ALL SELECT dock_assigned_at FROM shipments
            UNION ALL SELECT received_at FROM shipments
            UNION ALL SELECT applied_at FROM operations
            UNION ALL SELECT raised_at FROM alerts
            UNION ALL SELECT resolved_at FROM alerts
            UNION ALL SELECT issued_at FROM invoices
            UNION ALL SELECT paid_at FROM invoices
        ) AS instants
        """;

    [Fact]
    public async Task SecondStartOnSeededDatabaseAddsNothing()
    {
        string connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        int[] rowsAfterFirstStart;
        await using (TestDatabase first = await SeededDatabase.StartSeededAsync(connectionString, SeededDatabase.Now))
        {
            rowsAfterFirstStart = await first.InContextAsync(CountRowsAsync);
        }

        await using TestDatabase second =
            await SeededDatabase.StartSeededAsync(connectionString, SeededDatabase.Now.AddDays(1));
        int[] rowsAfterSecondStart = await second.InContextAsync(CountRowsAsync);

        Assert.Equal(rowsAfterFirstStart, rowsAfterSecondStart);
    }

    [Fact]
    public async Task EmptyDatabaseStaysEmptyWhenSeedIsNotEnabled()
    {
        string connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var database = new TestDatabase(connectionString);

        int[] rows = await database.InContextAsync(CountRowsAsync);

        Assert.Equal([0, 0, 0], rows);
    }

    [Fact]
    public async Task StartupFailsWhenSeedSettingIsNotBoolean()
    {
        string connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using var api = new ApiFactory(connectionString);
        await using WebApplicationFactory<Program> factory =
            api.WithWebHostBuilder(builder => builder.UseSetting("Seed:Enabled", "yes"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Seed:Enabled", exception.Message);
    }

    // Just after midnight in Toronto the anchor day ended five minutes ago: everything placed on it must already
    // be in the past.
    [Fact]
    public async Task NoInstantButScheduledDateIsLaterThanNowWhenSeededJustAfterMidnight()
    {
        var now = new DateTimeOffset(2026, 4, 18, 4, 5, 0, TimeSpan.Zero);
        string connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await using TestDatabase database = await SeededDatabase.StartSeededAsync(connectionString, now);

        DateTimeOffset? latest = await database.InContextAsync(db => db.Database
            .SqlQueryRaw<DateTimeOffset?>(LatestInstantSql)
            .SingleAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(latest);
        Assert.True(latest <= now, $"The latest seeded instant is {latest:O}, after {now:O}.");
    }

    private static async Task<int[]> CountRowsAsync(AppDbContext db)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        return
        [
            await db.Companies.CountAsync(cancellationToken),
            await db.Orders.CountAsync(cancellationToken),
            await db.Invoices.CountAsync(cancellationToken),
        ];
    }
}
