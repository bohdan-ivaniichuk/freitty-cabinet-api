namespace Freitty.Api.IntegrationTests.Dashboard;

public sealed class DashboardValidationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    // A number is refused as well as an unknown name: a client must not depend on the values behind the enum.
    [Theory]
    [InlineData("Year")]
    [InlineData("9")]
    public async Task UnknownPeriodIsABadRequestNamingIt(string period)
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.Dashboard}?period={period}");

        await ProblemResponse.AssertValidationProblemAsync(response, "period");
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
