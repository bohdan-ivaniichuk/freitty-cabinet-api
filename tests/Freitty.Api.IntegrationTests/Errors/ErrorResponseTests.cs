using System.Net;
using System.Text.Json;

namespace Freitty.Api.IntegrationTests.Errors;

public sealed class ErrorResponseTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task UnknownRouteUnderApiIsANotFoundProblem()
    {
        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Of("nothing-here"));

        await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingUserToActAsIsAServerErrorWithoutTheExceptionInTheBody()
    {
        string emptyDatabase = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(emptyDatabase);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(ApiUrls.Me, TestContext.Current.CancellationToken);
        JsonElement problem = await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.InternalServerError);

        string body = problem.GetRawText();
        Assert.DoesNotContain("Seed", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain("   at ", body);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
