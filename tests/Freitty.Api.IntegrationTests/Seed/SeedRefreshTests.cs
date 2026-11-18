using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Freitty.Api.Seed;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Domain.Carriers;
using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Freitty.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Freitty.Api.IntegrationTests.Seed;

// With Seed:Refresh on, each test has a database of its own: the refresh empties every table. The clock only moves when
// a test advances it; the refresher has no hook for tests, so a test that moves it asks until it sees the result.
public sealed class SeedRefreshTests(PostgresFixture postgres)
{
    private const string LoadedOrder = "FR001383";
    private const string ReadyOrder = "FR001679";
    private const int SeededVersion = 20;
    private const int LoadableStandard = 5;
    private const string OutboundTrailer = "TRL-8830";

    private const string RefuseCompaniesSql = """
        CREATE SEQUENCE refused_inserts;
        CREATE FUNCTION refuse_insert() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            PERFORM nextval('refused_inserts');
            RAISE EXCEPTION 'Refused by the test';
        END $$;
        CREATE TRIGGER refuse_companies BEFORE INSERT ON companies FOR EACH ROW EXECUTE FUNCTION refuse_insert();
        """;

    private const string AcceptCompaniesSql = "DROP TRIGGER refuse_companies ON companies";
    private const string RefusalCountSql = "SELECT is_called FROM refused_inserts";

    private static readonly TimeSpan HeldLongEnough = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollBound = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RefreshRebuildsANonEmptyDatabaseAtStartup()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using (TestDatabase seeded = await SeededDatabase.StartSeededAsync(connectionString, SeededDatabase.Now))
        {
            await seeded.SaveAsync(new Carrier(TestData.Unique("Carrier "), "+1 416 555 0100"));
        }

        await using var refreshed = new TestDatabase(Refreshing(connectionString, new FakeTimeProvider(Now)));
        int[] rows = await refreshed.InContextAsync(async db => new[]
        {
            await db.Companies.CountAsync(CancellationToken),
            await db.Carriers.CountAsync(CancellationToken),
            await db.Orders.CountAsync(CancellationToken),
        });

        Assert.Equal([1, 3, 71], rows);
    }

    [Fact]
    public async Task NextAnchorDayRebuildsTheSampleData()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        await using ApiFactory factory = Refreshing(await postgres.CreateDatabaseAsync(), clock);
        using HttpClient client = factory.CreateClient();
        await RecordLoadingAsync(client);
        DateTimeOffset? scheduledBefore = (await DetailAsync(client, ReadyOrder)).ScheduledAt;

        clock.Advance(SampleDataRefresher.CheckInterval);

        OrderDetailDto loaded = await EventuallyAsync(client, detail => detail.Version == SeededVersion);
        Assert.DoesNotContain(
            loaded.Shipments.SelectMany(shipment => shipment.Operations),
            operation => operation.Type == OperationType.Loading && operation.Quantity == LoadableStandard);
        Assert.Equal(scheduledBefore?.AddDays(1), (await DetailAsync(client, ReadyOrder)).ScheduledAt);
    }

    // The decision the timer acts on: the data is outdated once the anchor day moves on, and not before.
    [Fact]
    public async Task DataIsOutdatedOnlyOnceTheAnchorDayMovesOn()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        await using ApiFactory factory = Refreshing(await postgres.CreateDatabaseAsync(), clock);
        SampleData sampleData = factory.Services.GetRequiredService<SampleData>();

        clock.Advance(TimeSpan.FromMinutes(9));
        bool isOutdatedBeforeMidnight = sampleData.IsOutdated;
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(isOutdatedBeforeMidnight);
        Assert.True(sampleData.IsOutdated);
    }

    // A trigger the test adds refuses the seed's company after the tables were emptied: the rebuild fails as late as
    // it can. A sequence, which a rollback does not undo, counts the attempts.
    [Fact]
    public async Task FailedRebuildLeavesThePreviousDataAndIsTriedAgainAtTheNextCheck()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        string connectionString = await postgres.CreateDatabaseAsync();
        await using ApiFactory factory = Refreshing(connectionString, clock);
        using HttpClient client = factory.CreateClient();
        await RecordLoadingAsync(client);
        await ExecuteAsync(connectionString, RefuseCompaniesSql);

        clock.Advance(SampleDataRefresher.CheckInterval);
        await EventuallyAsync(() => HasRefusedAsync(connectionString));
        OrderDetailDto afterFailure = await DetailAsync(client, LoadedOrder);
        await ExecuteAsync(connectionString, AcceptCompaniesSql);
        clock.Advance(SampleDataRefresher.CheckInterval);

        Assert.Equal(SeededVersion + 1, afterFailure.Version);
        await EventuallyAsync(client, detail => detail.Version == SeededVersion);
    }

    // The gate is closed as a rebuild closes it: the request is held until it opens, then answered. Proving that it
    // is held takes a bounded wait in real time — far longer than the request takes when the gate is open.
    [Fact]
    public async Task RequestDuringARebuildWaitsForIt()
    {
        await using ApiFactory factory = Refreshing(await postgres.CreateDatabaseAsync(), new FakeTimeProvider(Now));
        using HttpClient client = factory.CreateClient();
        IDisposable closed = await factory.Services.GetRequiredService<RequestGate>().CloseAsync(CancellationToken);

        Task<OrderDetailDto> detail = DetailAsync(client, LoadedOrder);
        Task held = Task.Delay(HeldLongEnough, CancellationToken);
        Assert.Same(held, await Task.WhenAny(detail, held));
        closed.Dispose();

        Assert.Equal(SeededVersion, (await detail).Version);
    }

    [Fact]
    public async Task HealthProbeIsAnsweredWhileTheGateIsClosed()
    {
        await using ApiFactory factory = Refreshing(await postgres.CreateDatabaseAsync(), new FakeTimeProvider(Now));
        using HttpClient client = factory.CreateClient();
        using IDisposable closed =
            await factory.Services.GetRequiredService<RequestGate>().CloseAsync(CancellationToken);

        using HttpResponseMessage response = await client.GetAsync(ApiUrls.Liveness, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RefreshWithoutTheSeedRunsNothing()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using WebApplicationFactory<Program> factory = new ApiFactory(connectionString)
            .WithWebHostBuilder(builder => builder.UseSetting(ApiFactory.SeedRefreshKey, bool.TrueString));

        bool hasRefresher = factory.Services.GetServices<IHostedService>().OfType<SampleDataRefresher>().Any();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        bool hasCompany = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Companies.AnyAsync(CancellationToken);

        Assert.False(hasRefresher);
        Assert.False(hasCompany);
    }

    [Fact]
    public async Task StartupFailsWhenRefreshSettingIsNotBoolean()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using var api = new ApiFactory(connectionString, seedEnabled: true);
        await using WebApplicationFactory<Program> factory =
            api.WithWebHostBuilder(builder => builder.UseSetting(ApiFactory.SeedRefreshKey, "yes"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(ApiFactory.SeedRefreshKey, exception.Message);
    }

    private static DateTimeOffset Now => SeededDatabase.Now;

    // Ten minutes before the anchor day moves on: one check interval later it has, and the timer has ticked once.
    private static DateTimeOffset BeforeMidnight => Toronto.At(2026, 4, 17, 23, 50);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static ApiFactory Refreshing(string connectionString, FakeTimeProvider clock)
    {
        return new ApiFactory(connectionString, seedEnabled: true, clock, seedRefreshOn: true);
    }

    // The check runs on the refresher's thread once the clock has moved, so the test asks until it sees the result.
    // Each attempt is a real request, held at the gate while a rebuild runs; the bound only ends a test that would
    // otherwise hang.
    private static async Task<OrderDetailDto> EventuallyAsync(HttpClient client, Func<OrderDetailDto, bool> isDone)
    {
        OrderDetailDto? detail = null;
        await EventuallyAsync(async () => isDone(detail = await DetailAsync(client, LoadedOrder)));
        return detail!;
    }

    private static async Task EventuallyAsync(Func<Task<bool>> isDone)
    {
        var elapsed = Stopwatch.StartNew();
        while (!await isDone())
        {
            Assert.True(elapsed.Elapsed < PollBound, "The refresher did not act on the moved clock.");
            await Task.Yield();
        }
    }

    private static async Task RecordLoadingAsync(HttpClient client)
    {
        var loading = new OperationRequest(
            ShipmentPosition: 1,
            OperationType.Loading,
            UnitType.Standard,
            LoadableStandard,
            OutboundTrailer,
            IsBillable: true,
            PhotoCount: 0,
            SeededVersion);
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            ApiUrls.Operations(LoadedOrder),
            loading,
            ApiJson.Options,
            CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<OrderDetailDto> DetailAsync(HttpClient client, string number)
    {
        using HttpResponseMessage response = await client.GetAsync(ApiUrls.Order(number), CancellationToken);
        return await ApiJson.ReadAsync<OrderDetailDto>(response, HttpStatusCode.OK);
    }

    private static async Task<bool> HasRefusedAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new NpgsqlCommand(RefusalCountSql, connection);
        return (bool)(await command.ExecuteScalarAsync(CancellationToken))!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }
}
