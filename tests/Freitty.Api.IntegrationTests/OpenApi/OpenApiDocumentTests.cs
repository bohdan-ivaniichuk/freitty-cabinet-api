using System.Net;
using System.Text.Json;

namespace Freitty.Api.IntegrationTests.OpenApi;

public sealed class OpenApiDocumentTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly string[] HttpMethods =
        ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    private readonly CabinetApi api = new(postgres.ConnectionString);

    public static TheoryData<string> FilteredPaths => [ApiUrls.Orders, ApiUrls.OrderCounts];

    [Fact]
    public async Task OpenApiDocumentListsEveryEndpoint()
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        (string Path, string Method)[] operations = document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(member => HttpMethods.Contains(member.Name))
                .Select(member => (path.Name, member.Name)))
            .ToArray();
        Assert.Equivalent(
            new[]
            {
                (ApiUrls.Orders, "get"),
                (ApiUrls.OrderCounts, "get"),
                (ApiUrls.OrderTemplate, "get"),
                (ApiUrls.OperationsTemplate, "post"),
                (ApiUrls.Hubs, "get"),
                (ApiUrls.Me, "get"),
                (ApiUrls.Dashboard, "get"),
            },
            operations,
            strict: true);
    }

    [Fact]
    public async Task OpenApiDocumentDescribesTheDetailAndItsNotFoundProblem()
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        JsonElement responses = document.GetProperty("paths").GetProperty(ApiUrls.OrderTemplate)
            .GetProperty("get").GetProperty("responses");
        Assert.Equal(
            "#/components/schemas/OrderDetailDto",
            responses.GetProperty("200").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal(
            "#/components/schemas/DomainProblemDetails",
            responses.GetProperty("404").GetProperty("content").GetProperty(ProblemResponse.ContentType)
                .GetProperty("schema").GetProperty("$ref").GetString());
        JsonElement problem = document.GetProperty("components").GetProperty("schemas")
            .GetProperty("DomainProblemDetails");
        Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
        Assert.Contains("code", problem.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentDescribesRecordingAnOperation()
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        JsonElement post = document.GetProperty("paths").GetProperty(ApiUrls.OperationsTemplate)
            .GetProperty("post");
        Assert.Equal(
            "#/components/schemas/RecordOperationRequest",
            post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        JsonElement responses = post.GetProperty("responses");
        Assert.Equal(["201", "400", "404", "409"], responses.EnumerateObject().Select(status => status.Name));
        Assert.Equal(
            "#/components/schemas/RecordedOperationDto",
            responses.GetProperty("201").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.All(
            new[] { "404", "409" },
            status => Assert.Equal(
                "#/components/schemas/DomainProblemDetails",
                responses.GetProperty(status).GetProperty("content").GetProperty(ProblemResponse.ContentType)
                    .GetProperty("schema").GetProperty("$ref").GetString()));
        JsonElement request = document.GetProperty("components").GetProperty("schemas")
            .GetProperty("RecordOperationRequest");
        Assert.Equal(
            ["shipmentPosition", "type", "unitType", "quantity", "expectedVersion"],
            request.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentRequiresNoListParameter()
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        JsonElement parameters = document.GetProperty("paths").GetProperty(ApiUrls.Orders)
            .GetProperty("get").GetProperty("parameters");
        Assert.All(
            parameters.EnumerateArray(),
            parameter => Assert.False(
                parameter.TryGetProperty("required", out JsonElement required) && required.GetBoolean(),
                parameter.GetProperty("name").GetString()));
    }

    [Theory]
    [MemberData(nameof(FilteredPaths))]
    public async Task OpenApiDocumentDescribesEveryFilterOfTheList(string path)
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        JsonElement parameters = document.GetProperty("paths").GetProperty(path).GetProperty("get")
            .GetProperty("parameters");
        Assert.NotEmpty(parameters.EnumerateArray());
        Assert.All(
            parameters.EnumerateArray(),
            parameter => Assert.False(
                string.IsNullOrWhiteSpace(
                    parameter.TryGetProperty("description", out JsonElement description)
                        ? description.GetString()
                        : null),
                $"{parameter.GetProperty("name").GetString()} has no description."));
    }

    [Fact]
    public async Task OpenApiDocumentDescribesEnumsAsStrings()
    {
        JsonElement document = await ReadOpenApiDocumentAsync();

        JsonElement status = document.GetProperty("components").GetProperty("schemas").GetProperty("OrderStatus");
        Assert.Equal("string", status.GetProperty("type").GetString());
        Assert.Equal(
            ["Draft", "Ready", "InProgress", "Completed"],
            status.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task ScalarServesItsPage()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/scalar/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private Task<JsonElement> ReadOpenApiDocumentAsync()
    {
        return api.GetAsync<JsonElement>("/openapi/v1.json");
    }
}
