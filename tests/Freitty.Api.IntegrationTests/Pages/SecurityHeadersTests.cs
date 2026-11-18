using System.Net;

namespace Freitty.Api.IntegrationTests.Pages;

public sealed class SecurityHeadersTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string ContentSecurityPolicy = "Content-Security-Policy";

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Theory]
    [InlineData("/")]
    [InlineData("/orders.html")]
    [InlineData("/order.html")]
    public async Task PageCarriesEverySecurityHeader(string url)
    {
        using HttpResponseMessage response = await api.SendGetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCommonHeaders(response);
        string policy = Assert.Single(response.Headers.GetValues(ContentSecurityPolicy));
        Assert.Contains("script-src 'self'", policy);
        Assert.Contains("frame-ancestors 'none'", policy);
    }

    [Fact]
    public async Task ApiResponseCarriesTheCommonHeadersButNoPagePolicy()
    {
        await api.SaveReferenceDataAsync();

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCommonHeaders(response);
        Assert.False(response.Headers.Contains(ContentSecurityPolicy));
    }

    [Fact]
    public async Task ApiReferenceKeepsTheBrowserDefaults()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/scalar/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains(ContentSecurityPolicy));
    }

    [Fact]
    public async Task ErrorResponseCarriesTheCommonHeaders()
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.Orders}?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertCommonHeaders(response);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static void AssertCommonHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
}
