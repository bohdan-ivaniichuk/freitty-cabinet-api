using System.Net;
using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Freitty.Api.IntegrationTests.Support;

// A database of its own, seeded once and only read by the tests that share it. The clock stands at 09:00 in
// Toronto on 18 April 2026, so the anchor day is 17 April, the mockup's own "today".
public sealed class SeededDatabase(PostgresFixture postgres) : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 4, 18, 13, 0, 0, TimeSpan.Zero);

    private TestDatabase? database;
    private HttpClient? client;

    public async ValueTask InitializeAsync()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        ApiFactory factory = Seeding(connectionString, Now);
        database = new TestDatabase(factory);

        // The API seeds while it starts; the client is created once the seed is in.
        client = factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    public Task<T> InContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        return Database.InContextAsync(action);
    }

    // As the API that seeded the database answers: its clock and its current user, the seeded Admin.
    public async Task<T> GetAsync<T>(string url)
    {
        HttpClient started = client
            ?? throw new InvalidOperationException("The seeded API is started in InitializeAsync.");
        using HttpResponseMessage response = await started.GetAsync(url, TestContext.Current.CancellationToken);
        return await ApiJson.ReadAsync<T>(response, HttpStatusCode.OK);
    }

    public Task<Order> LoadOrderAsync(string number)
    {
        return InContextAsync(async db =>
        {
            Guid orderId = await db.Orders
                .Where(order => order.Number == number)
                .Select(order => order.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
            return await db.LoadOrderAsync(orderId);
        });
    }

    // The API seeds while it starts, so the first use of its services waits for the seed.
    internal static async Task<TestDatabase> StartSeededAsync(string connectionString, DateTimeOffset now)
    {
        var started = new TestDatabase(Seeding(connectionString, now));
        await started.InContextAsync(_ => Task.CompletedTask);
        return started;
    }

    private static ApiFactory Seeding(string connectionString, DateTimeOffset now)
    {
        return new ApiFactory(connectionString, seedEnabled: true, new FakeTimeProvider(now));
    }

    private TestDatabase Database => database
        ?? throw new InvalidOperationException("The seeded database is created in InitializeAsync.");
}
