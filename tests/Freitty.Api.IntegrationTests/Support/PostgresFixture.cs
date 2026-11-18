using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Freitty.Api.IntegrationTests.Support;

// One container per test run, shared by all test classes. The image tag matches docker-compose.yml;
// change both together.
//
// This and every other helper of the tests reads xUnit's TestContext.Current.CancellationToken instead of taking a
// token, a departure from rule 14: xUnit owns the token, and passing it through each call would only repeat it.
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
    public async Task<string> CreateDatabaseAsync()
    {
        string databaseName = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE {databaseName}");
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName }.ConnectionString;
    }

    // WITH (FORCE) ends the connections the API still holds, as an outage would.
    public async Task DropDatabaseAsync(string connectionString)
    {
        string? databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await ExecuteAsync($"DROP DATABASE {databaseName} WITH (FORCE)");
    }

    // The database name comes from a Guid, not from input: CREATE and DROP DATABASE take no parameters.
    private async Task ExecuteAsync(string sql)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
