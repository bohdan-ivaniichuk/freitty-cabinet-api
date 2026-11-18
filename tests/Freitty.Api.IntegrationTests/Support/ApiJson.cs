using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Freitty.Api.IntegrationTests.Support;

// How a client reads the API: the web defaults of System.Text.Json and enums as strings — only as strings, so a
// response that sends an enum as its number fails the test reading it.
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    // The body is shown when the status is not the expected one: a 400 or a 500 says why.
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.StatusCode == expectedStatus,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery} answered "
                + $"{response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<T>(Options, cancellationToken))!;
    }
}
