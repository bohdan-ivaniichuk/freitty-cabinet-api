using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Freitty.Api.IntegrationTests.Seed;

// A database of its own, seeded once and only read by the tests that share it. The clock stands at 09:00 in
// Toronto on 18 April 2026, so the anchor day is 17 April, the mockup's own "today".
public sealed class SeededDatabase(PostgresFixture postgres) : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 4, 18, 13, 0, 0, TimeSpan.Zero);

    private TestDatabase? database;

    public async ValueTask InitializeAsync()
    {
        string connectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        database = await StartSeededAsync(connectionString, Now);
    }

    public async ValueTask DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    public Task<T> InContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        return Database.InContextAsync(action);
    }

    // A client of the API that seeded the database: its clock and its current user, the seeded Admin.
    public HttpClient CreateClient()
    {
        return Database.CreateClient();
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
        var started = new TestDatabase(new ApiFactory(connectionString, seedEnabled: true, new FakeTimeProvider(now)));
        await started.InContextAsync(_ => Task.CompletedTask);
        return started;
    }

    private TestDatabase Database => database
        ?? throw new InvalidOperationException("The seeded database is created in InitializeAsync.");
}
