using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Freitty.Application.Orders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class RecordOperationValidationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    // Each change is applied to a valid Unloading; a property set to null is left out of the body.
    [Theory]
    [InlineData("""{ "shipmentPosition": null }""", "shipmentPosition")]
    [InlineData("""{ "shipmentPosition": 0 }""", "shipmentPosition")]
    [InlineData("""{ "type": null }""", "type")]
    [InlineData("""{ "type": "Pallet" }""", "type")]
    [InlineData("""{ "type": 9 }""", "type")]
    [InlineData("""{ "unitType": null }""", "unitType")]
    [InlineData("""{ "unitType": "Crate" }""", "unitType")]
    [InlineData("""{ "unitType": 3 }""", "unitType")]
    [InlineData("""{ "quantity": null }""", "quantity")]
    [InlineData("""{ "quantity": 0 }""", "quantity")]
    [InlineData("""{ "quantity": 10001 }""", "quantity")]
    [InlineData("""{ "quantity": "many" }""", "quantity")]
    [InlineData("""{ "quantity": 1.5 }""", "quantity")]
    [InlineData("""{ "trailerNumber": null }""", "trailerNumber")]
    [InlineData("""{ "trailerNumber": "" }""", "trailerNumber")]
    [InlineData("""{ "trailerNumber": "   " }""", "trailerNumber")]
    [InlineData("""{ "trailerNumber": "TL\u00001" }""", "trailerNumber")]
    [InlineData("""{ "trailerNumber": "TRL-12345678901234567" }""", "trailerNumber")]
    [InlineData("""{ "type": "Loading", "trailerNumber": null }""", "trailerNumber")]
    [InlineData("""{ "type": "Disposal" }""", "trailerNumber")]
    [InlineData("""{ "type": "Restack" }""", "trailerNumber")]
    [InlineData("""{ "type": "Disposal", "trailerNumber": "" }""", "trailerNumber")]
    [InlineData("""{ "photoCount": -1 }""", "photoCount")]
    [InlineData("""{ "photoCount": 100 }""", "photoCount")]
    [InlineData("""{ "expectedVersion": null }""", "expectedVersion")]
    [InlineData("""{ "expectedVersion": 0 }""", "expectedVersion")]
    public async Task InvalidFieldIsABadRequestNamingItAndWritesNothing(string change, string field)
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.SendPostAsync(
            OperationRequest.UrlOf(order.Number),
            Changed(OperationRequest.Unloading(order, 1), change));
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(field, Assert.Single(problem.GetProperty("errors").EnumerateArray()).GetProperty("name").GetString());
        AssertNamesNoDotNetType(problem);
        Assert.Equal(order.Version, (await api.DetailAsync(order.Number)).Version);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"Unloading\"")]
    [InlineData("42")]
    public async Task BodyThatIsNotAnObjectIsABadRequestOfTheWholeBody(string json)
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.SendPostAsync(OperationRequest.UrlOf(order.Number), json);
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "serializerErrors",
            Assert.Single(problem.GetProperty("errors").EnumerateArray()).GetProperty("name").GetString());
        AssertNamesNoDotNetType(problem);
    }

    // Each defined type, with and without a trailer, is either recorded or refused with a 4xx: the validator keeps
    // every request the aggregate would reject as a broken contract from reaching it.
    [Theory]
    [MemberData(nameof(EveryTypeWithAndWithoutTrailer))]
    public async Task NoOperationTypeReachesTheAggregateWithABrokenContract(OperationType type, bool withTrailer)
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);
        OperationRequest request = OperationRequest.Unloading(order, 1) with
        {
            Type = type,
            TrailerNumber = withTrailer ? OrderBuilder.TrailerNumber : null,
        };

        using HttpResponseMessage response = await api.SendPostAsync(OperationRequest.UrlOf(order.Number), request);

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest,
            $"{type} {(withTrailer ? "with" : "without")} a trailer answered {response.StatusCode}.");
    }

    [Fact]
    public async Task ValuesAtTheLimitsAreAccepted()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        OperationRequest atLimits = OperationRequest.Unloading(order, 10_000, photos: 99) with
        {
            TrailerNumber = "TRL-1234567890123456",
        };

        using HttpResponseMessage response = await api.SendPostAsync(OperationRequest.UrlOf(order.Number), atLimits);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OptionalFieldsDefaultToNotBillableAndNoPhoto()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);
        JsonObject restack = Changed(
            OperationRequest.Unloading(order, 1) with { Type = OperationType.Restack, TrailerNumber = null },
            """{ "isBillable": null, "photoCount": null, "trailerNumber": null }""");

        using HttpResponseMessage response = await api.SendPostAsync(OperationRequest.UrlOf(order.Number), restack);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OperationDto operation = Assert.Single(
            Assert.Single((await api.DetailAsync(order.Number)).Shipments).Operations,
            operation => operation.Type == OperationType.Restack);
        Assert.Equal((false, 0), (operation.IsBillable, operation.PhotoCount));
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    public static TheoryData<OperationType, bool> EveryTypeWithAndWithoutTrailer()
    {
        var cases = new TheoryData<OperationType, bool>();
        foreach (OperationType type in Enum.GetValues<OperationType>())
        {
            cases.Add(type, true);
            cases.Add(type, false);
        }

        return cases;
    }

    private static void AssertNamesNoDotNetType(JsonElement problem)
    {
        string body = problem.GetRawText();
        Assert.DoesNotContain("Freitty.", body);
        Assert.DoesNotContain("Int32", body);
    }

    private static JsonObject Changed(OperationRequest request, string change)
    {
        JsonObject body = JsonSerializer.SerializeToNode(request, ApiJson.Options)!.AsObject();
        foreach ((string name, JsonNode? value) in JsonNode.Parse(change)!.AsObject())
        {
            if (value is null)
            {
                body.Remove(name);
            }
            else
            {
                body[name] = value.DeepClone();
            }
        }

        return body;
    }
}
