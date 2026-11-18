namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderListValidationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    [InlineData("pageSize=many", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("page=10001", "page")]
    [InlineData("type=Pallet", "type")]
    [InlineData("status=Done", "status")]
    [InlineData("status=99", "status")]
    [InlineData("datePreset=Yesterday", "datePreset")]
    [InlineData("hubId=Markham", "hubId")]
    [InlineData("hubId=", "hubId")]
    [InlineData("hasOpenAlerts=maybe", "hasOpenAlerts")]
    [InlineData("search=ab", "search")]
    [InlineData("search=%20ab%20%20", "search")]
    [InlineData("search=abc%00", "search")]
    [InlineData("search=%00%00%00", "search")]
    [InlineData("search=ab%09cd", "search")]
    public async Task InvalidListParameterIsABadRequestNamingTheField(string query, string field)
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.Orders}?{query}");

        await ProblemResponse.AssertValidationProblemAsync(response, field);
    }

    [Theory]
    [InlineData("status=Done", "status")]
    [InlineData("hubId=Markham", "hubId")]
    [InlineData("hubId=", "hubId")]
    [InlineData("search=ab", "search")]
    [InlineData("search=ab%00c", "search")]
    public async Task InvalidCountersParameterIsABadRequestNamingTheField(string query, string field)
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.OrderCounts}?{query}");

        await ProblemResponse.AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task SearchLongerThanFiftyCharactersIsABadRequest()
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.Orders}?search={new string('x', 51)}");

        await ProblemResponse.AssertValidationProblemAsync(response, "search");
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
