using System.Net;

namespace Freitty.Api.IntegrationTests.Pages;

public sealed class StaticPagesTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string Html = "text/html";
    private const string JavaScript = "text/javascript";
    private const string RevalidateEveryTime = "no-cache";
    private const string DashboardTitle = "<title>Freitty — Dashboard</title>";

    private readonly CabinetApi api = new(postgres.ConnectionString);

    public static TheoryData<string> UrlsBesideThePages => ["/health/ready", ApiUrls.Me];

    [Theory]
    [InlineData("/", Html)]
    [InlineData("/index.html", Html)]
    [InlineData("/orders.html", Html)]
    [InlineData("/order.html", Html)]
    [InlineData("/app.css", "text/css")]
    [InlineData("/js/api.js", JavaScript)]
    [InlineData("/js/format.js", JavaScript)]
    [InlineData("/js/shell.js", JavaScript)]
    [InlineData("/js/card.js", JavaScript)]
    [InlineData("/js/dashboard.js", JavaScript)]
    [InlineData("/js/orders.js", JavaScript)]
    [InlineData("/js/order.js", JavaScript)]
    public async Task EveryFileOfThePagesIsServedWithItsTypeForRevalidationOnEveryLoad(string url, string mediaType)
    {
        using HttpResponseMessage response = await api.SendGetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(RevalidateEveryTime, response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task RootServesTheDashboard()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(DashboardTitle, body);
    }

    [Fact]
    public async Task MissingFileIsTheNotFoundProblem()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/missing.html");

        await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(UrlsBesideThePages))]
    public async Task ApiStillAnswersBesideThePages(string url)
    {
        await api.SaveReferenceDataAsync();

        using HttpResponseMessage response = await api.SendGetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
