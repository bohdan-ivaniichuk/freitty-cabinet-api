using System.Net;
using System.Text.Json;

namespace Freitty.Api.IntegrationTests.Support;

// Every error of the API is a ProblemDetails; a test reads its body only after checking that it is one.
internal static class ProblemResponse
{
    public const string ContentType = "application/problem+json";

    // The type ASP.NET Core gives a 400; the API gives it to the 400s FastEndpoints writes too.
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(ContentType, response.Content.Headers.ContentType?.MediaType);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    // Any error: its status in the response and in the body, and the path of the request it answers.
    public static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        JsonElement problem = await ReadAsync(response);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(response.RequestMessage?.RequestUri?.AbsolutePath, problem.GetProperty("instance").GetString());
        AssertNamesNoDotNetType(problem);
        return problem;
    }

    // A refusal of the domain: the code a client branches on and the detail naming what was refused.
    public static async Task AssertDomainProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string detail)
    {
        JsonElement problem = await AssertProblemAsync(response, status);

        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
    }

    // A refused input: one error naming the field, and the detail repeating its reason for a client that shows one.
    public static async Task<JsonElement> AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);

        Assert.Equal(BadRequestType, problem.GetProperty("type").GetString());
        JsonElement error = Assert.Single(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal(field, error.GetProperty("name").GetString());
        Assert.Equal(error.GetProperty("reason").GetString(), problem.GetProperty("detail").GetString());
        return problem;
    }

    // The messages of System.Text.Json and of the binder name .NET types; a client cannot act on them.
    private static void AssertNamesNoDotNetType(JsonElement problem)
    {
        string body = problem.GetRawText();
        Assert.DoesNotContain("Freitty.", body);
        Assert.DoesNotContain("Int32", body);
    }
}
