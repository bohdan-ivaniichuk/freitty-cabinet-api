using System.Net;

namespace Freitty.Api.IntegrationTests.Health;

public sealed class HealthEndpointsTests(PostgresFixture postgres)
{
    private const string LivenessUrl = "/health/live";
    private const string ReadinessUrl = "/health/ready";

    // Nothing listens on port 1; the short timeout keeps the failing check fast.
    private const string UnreachableConnectionString =
        "Host=localhost;Port=1;Database=freitty;Username=freitty;Timeout=1";

    [Fact]
    public async Task ReadinessIsHealthyWhenPostgresIsReachable()
    {
        HttpStatusCode statusCode = await GetStatusCodeAsync(postgres.ConnectionString, ReadinessUrl);

        Assert.Equal(HttpStatusCode.OK, statusCode);
    }

    [Fact]
    public async Task ReadinessIsUnavailableWhenPostgresIsUnreachable()
    {
        HttpStatusCode statusCode = await GetStatusCodeAsync(UnreachableConnectionString, ReadinessUrl);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, statusCode);
    }

    [Fact]
    public async Task LivenessIsHealthyWhenPostgresIsUnreachable()
    {
        HttpStatusCode statusCode = await GetStatusCodeAsync(UnreachableConnectionString, LivenessUrl);

        Assert.Equal(HttpStatusCode.OK, statusCode);
    }

    private static async Task<HttpStatusCode> GetStatusCodeAsync(string connectionString, string url)
    {
        await using var factory = new ApiFactory(connectionString);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }
}
