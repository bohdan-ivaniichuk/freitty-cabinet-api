using System.Net;

namespace Freitty.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests(PostgresFixture postgres)
{
    private const string ReadinessUrl = "/health/ready";

    [Fact]
    public async Task ReadinessIsHealthyWhenPostgresIsReachable()
    {
        await using var factory = new ApiFactory(postgres.ConnectionString);
        using HttpClient client = factory.CreateClient();

        HttpStatusCode statusCode = await GetStatusCodeAsync(client, ReadinessUrl);

        Assert.Equal(HttpStatusCode.OK, statusCode);
    }

    [Fact]
    public async Task ReadinessIsUnavailableAfterDatabaseGoesAway()
    {
        HttpStatusCode statusCode = await GetStatusCodeAfterDatabaseGoesAwayAsync(ReadinessUrl);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, statusCode);
    }

    [Fact]
    public async Task LivenessIsHealthyAfterDatabaseGoesAway()
    {
        HttpStatusCode statusCode = await GetStatusCodeAfterDatabaseGoesAwayAsync(ApiUrls.Liveness);

        Assert.Equal(HttpStatusCode.OK, statusCode);
    }

    // The API migrates its database at startup and does not start without one, so the outage is staged after
    // startup on a database created for this test.
    private async Task<HttpStatusCode> GetStatusCodeAfterDatabaseGoesAwayAsync(string url)
    {
        string connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(connectionString);
        using HttpClient client = factory.CreateClient();

        await postgres.DropDatabaseAsync(connectionString);

        return await GetStatusCodeAsync(client, url);
    }

    private static async Task<HttpStatusCode> GetStatusCodeAsync(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
