using System.Net;
using System.Text.Json;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class ApiContractTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Theory]
    [InlineData("/api/v1/orders?pageSize=0", "pageSize")]
    [InlineData("/api/v1/orders?pageSize=51", "pageSize")]
    [InlineData("/api/v1/orders?pageSize=many", "pageSize")]
    [InlineData("/api/v1/orders?page=0", "page")]
    [InlineData("/api/v1/orders?page=10001", "page")]
    [InlineData("/api/v1/orders?type=Pallet", "type")]
    [InlineData("/api/v1/orders?status=Done", "status")]
    [InlineData("/api/v1/orders?status=99", "status")]
    [InlineData("/api/v1/orders?date=Yesterday", "date")]
    [InlineData("/api/v1/orders?hub=Markham", "hub")]
    [InlineData("/api/v1/orders?hub=", "hub")]
    [InlineData("/api/v1/orders?alerts=maybe", "alerts")]
    [InlineData("/api/v1/orders?q=ab", "q")]
    [InlineData("/api/v1/orders?q=%20ab%20%20", "q")]
    [InlineData("/api/v1/orders?q=abc%00", "q")]
    [InlineData("/api/v1/orders?q=%00%00%00", "q")]
    [InlineData("/api/v1/orders?q=ab%09cd", "q")]
    [InlineData("/api/v1/orders/counts?status=Done", "status")]
    [InlineData("/api/v1/orders/counts?hub=Markham", "hub")]
    [InlineData("/api/v1/orders/counts?hub=", "hub")]
    [InlineData("/api/v1/orders/counts?q=ab", "q")]
    [InlineData("/api/v1/orders/counts?q=ab%00c", "q")]
    [InlineData("/api/v1/dashboard?period=Year", "period")]
    [InlineData("/api/v1/dashboard?period=9", "period")]
    public async Task InvalidParameterIsABadRequestNamingTheField(string url, string field)
    {
        using HttpResponseMessage response = await api.SendGetAsync(url);
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(field, problem.GetProperty("errors")[0].GetProperty("name").GetString());
        Assert.Equal(
            problem.GetProperty("errors")[0].GetProperty("reason").GetString(),
            problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("Freitty.", problem.GetRawText());
        Assert.DoesNotContain("Int32", problem.GetRawText());
    }

    [Fact]
    public async Task SearchLongerThanFiftyCharactersIsABadRequest()
    {
        using HttpResponseMessage response = await api.SendGetAsync($"/api/v1/orders?q={new string('x', 51)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRouteUnderApiIsANotFoundProblem()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/api/v1/nothing-here");
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task OpenApiDocumentListsTheSixEndpoints()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/openapi/v1.json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        string[] paths = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => path.Value.TryGetProperty("get", out _))
            .Select(path => path.Name)
            .ToArray();
        Assert.Equivalent(
            new[]
            {
                "/api/v1/orders", "/api/v1/orders/counts", "/api/v1/orders/{number}", "/api/v1/hubs", "/api/v1/me",
                "/api/v1/dashboard",
            },
            paths,
            strict: true);
    }

    [Fact]
    public async Task OpenApiDocumentDescribesTheDetailAndItsNotFoundProblem()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/openapi/v1.json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement responses = document.RootElement.GetProperty("paths").GetProperty("/api/v1/orders/{number}")
            .GetProperty("get").GetProperty("responses");
        Assert.Equal(
            "#/components/schemas/OrderDetailDto",
            responses.GetProperty("200").GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal(
            "#/components/schemas/DomainProblemDetails",
            responses.GetProperty("404").GetProperty("content").GetProperty(ProblemResponse.ContentType)
                .GetProperty("schema").GetProperty("$ref").GetString());
        JsonElement problem = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("DomainProblemDetails");
        Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
        Assert.Contains("code", problem.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentDescribesRecordingAnOperation()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/openapi/v1.json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement post = document.RootElement.GetProperty("paths").GetProperty("/api/v1/orders/{number}/operations")
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
        JsonElement request = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("RecordOperationRequest");
        Assert.Equal(
            ["shipmentPosition", "type", "unitType", "quantity", "expectedVersion"],
            request.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentRequiresNoListParameter()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/openapi/v1.json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement parameters = document.RootElement.GetProperty("paths").GetProperty("/api/v1/orders")
            .GetProperty("get").GetProperty("parameters");
        Assert.All(
            parameters.EnumerateArray(),
            parameter => Assert.False(
                parameter.TryGetProperty("required", out JsonElement required) && required.GetBoolean(),
                parameter.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentDescribesEnumsAsStrings()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/openapi/v1.json");
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement status = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("OrderStatus");
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

    [Fact]
    public async Task MissingUserToActAsIsAServerErrorWithoutTheExceptionInTheBody()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string emptyDatabase = await postgres.CreateDatabaseAsync(cancellationToken);
        await using var factory = new ApiFactory(emptyDatabase);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/me", cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemResponse.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Seed", body);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
