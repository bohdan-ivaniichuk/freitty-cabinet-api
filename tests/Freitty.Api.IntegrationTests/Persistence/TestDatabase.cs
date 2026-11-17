using Freitty.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Persistence;

// There is no endpoint yet, so these tests take AppDbContext from the services of the started API instead of
// calling HTTP as ADR 0002 describes. Every call gets a new context, so a read sees only what was saved.
internal sealed class TestDatabase(ApiFactory factory) : IAsyncDisposable
{
    public TestDatabase(string connectionString)
        : this(new ApiFactory(connectionString))
    {
    }

    public async Task InContextAsync(Func<AppDbContext, Task> action)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> InContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task SaveAsync(params object[] entities)
    {
        return InContextAsync(async db =>
        {
            db.AddRange(entities);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
    }
}
