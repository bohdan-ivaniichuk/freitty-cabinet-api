using Freitty.Api.IntegrationTests;
using Npgsql;
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

    // For a test that needs a database of its own: an empty one, or one it is going to drop.
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        string databaseName = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE {databaseName}", cancellationToken);
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName }.ConnectionString;
    }

    // WITH (FORCE) ends the connections the API still holds, as an outage would.
    public async Task DropDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        string? databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await ExecuteAsync($"DROP DATABASE {databaseName} WITH (FORCE)", cancellationToken);
    }

    // The database name comes from a Guid, not from input: CREATE and DROP DATABASE take no parameters.
    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
