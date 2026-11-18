using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Freitty.Api.IntegrationTests.Pages;

// A browser and a JSON client asking for the same missing address. Redirects are not followed, so a test sees them.
public sealed class NotFoundPageTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string BrowserAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    private const string JsonAccept = "application/json";
    private const string NotFoundTitle = "<title>Freitty — Page not found</title>";

    private readonly ApiFactory factory = new(postgres.ConnectionString);
    private HttpClient? client;

    [Theory]
    [InlineData("/orders?status=Draft&page=2", "/orders.html?status=Draft&page=2")]
    [InlineData("/order?n=FR001383", "/order.html?n=FR001383")]
    [InlineData("/Orders", "/orders.html")]
    public async Task PageAddressWithoutItsExtensionRedirectsKeepingTheQuery(string url, string location)
    {
        using HttpResponseMessage response = await GetAsync(url, BrowserAccept);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(location, response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/nonexistent")]
    [InlineData("/missing.html")]
    [InlineData("/favicon.ico")]
    public async Task BrowserAskingForAMissingAddressGetsThePage(string url)
    {
        using HttpResponseMessage response = await GetAsync(url, BrowserAccept);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(NotFoundTitle, await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Theory]
    [InlineData("/nonexistent", JsonAccept)]
    [InlineData("/nonexistent", null)]
    [InlineData("/api/v1/nonexistent", BrowserAccept)]
    [InlineData("/health/nonexistent", BrowserAccept)]
    public async Task EveryOtherMissingAddressIsTheNotFoundProblem(string url, string? accept)
    {
        using HttpResponseMessage response = await GetAsync(url, accept);

        await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await factory.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private async Task<HttpResponseMessage> GetAsync(string url, string? accept)
    {
        client ??= factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (accept is not null)
        {
            request.Headers.Accept.ParseAdd(accept);
        }

        return await client.SendAsync(request, CancellationToken);
    }
}
