using Freitty.Api.IntegrationTests;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Freitty.Api.IntegrationTests;

// One container per test run, shared by all test classes. The image tag matches docker-compose.yml;
// change both together.
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Image = "postgres:18-alpine";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder(Image).Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }
}
