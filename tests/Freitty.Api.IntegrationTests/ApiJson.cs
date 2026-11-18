using System.Text.Json;
using System.Text.Json.Serialization;

namespace Freitty.Api.IntegrationTests;

// How a client reads the API: the web defaults of System.Text.Json and enums as strings — only as strings, so a
// response that sends an enum as its number fails the test reading it.
internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };
}
