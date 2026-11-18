using Freitty.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Support;

// AppDbContext from the services of the started API: persistence tests read and write through it, and HTTP tests
// arrange their data with it. Every call gets a new context, so a read sees only what was saved.
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

    public Task<T> InContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        return InScopeAsync(services => action(services.GetRequiredService<AppDbContext>()));
    }

    // For a test that calls an application service directly, without HTTP.
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
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
