using System.Text.Json;

namespace Freitty.Api.IntegrationTests;

// Every error of the API is a ProblemDetails; a test reads its body only after checking that it is one.
internal static class ProblemResponse
{
    public const string ContentType = "application/problem+json";

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(ContentType, response.Content.Headers.ContentType?.MediaType);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
