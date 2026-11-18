using System.Text.RegularExpressions;
using Npgsql;

namespace Freitty.Api.IntegrationTests.Startup;

public sealed class StartupTests(PostgresFixture postgres)
{
    // Port 1 has no listener: a connection is refused at once.
    private const int UnusedPort = 1;
    private const string TableCountSql = "SELECT count(*) FROM pg_tables WHERE schemaname = 'public'";

    [Fact]
    public void StartupFailsNamingTheEnvironmentVariableWhenConnectionStringIsMissing()
    {
        using var factory = new ApiFactory(connectionString: string.Empty);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings__Postgres", exception.Message);
    }

    [Fact]
    public void StartupFailsNamingTheEnvironmentVariableWhenPhotoStorageIsMissing()
    {
        using var factory = new ApiFactory(postgres.ConnectionString, photoStorageConnectionString: string.Empty);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings__PhotoStorage", exception.Message);
    }

    // The storage is prepared before the migrations, so a storage that cannot be reached leaves the schema as it was.
    [Fact]
    public async Task UnreachablePhotoStorageStopsTheStartupBeforeTheMigrations()
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        string unreachable = Regex.Replace(
            AzuriteFixture.ConnectionString,
            "BlobEndpoint=http://[^/]+/",
            $"BlobEndpoint=http://127.0.0.1:{UnusedPort}/");
        using var factory = new ApiFactory(connectionString, photoStorageConnectionString: unreachable);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(TableCountSql, connection);
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Eastern Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    public void StartupFailsNamingTheEnvironmentVariableWhenReportingTimeZoneIsNotAnIanaId(string timeZone)
    {
        using var factory = new ApiFactory(postgres.ConnectionString, reportingTimeZone: timeZone);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Cabinet__TimeZone", exception.Message);
    }
}
