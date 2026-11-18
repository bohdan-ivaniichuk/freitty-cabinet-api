using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Orders;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.RecordOperation;
using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class RecordOperationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task FirstOperationStartsAReadyOrder()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(order.Number, OperationRequest.Unloading(order, 4));
        RecordedOperationDto recorded = await ReadCreatedAsync(response);

        Assert.Equal(new RecordedOrderDto(order.Number, OrderStatus.InProgress, order.Version + 1), recorded.Order);
        Assert.Equal(
            new OperationDto(
                recorded.Operation.Id,
                OperationType.Unloading,
                UnitType.Standard,
                4,
                OrderBuilder.TrailerNumber,
                CabinetApi.Now,
                IsBillable: true,
                PhotoCount: 1,
                CommentCount: 0),
            recorded.Operation);
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Equal(OrderStatus.InProgress, detail.Status);
        Assert.Equal(recorded.Operation, Assert.Single(Assert.Single(detail.Shipments).Operations));
    }

    [Fact]
    public async Task LocationIsTheOrderUnderItsStoredNumber()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number.ToLowerInvariant(),
            OperationRequest.Unloading(order, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/v1/orders/{order.Number}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnloadingAboveTheDeclaredQuantityOpensAQuantityDeltaAlert()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Unloading(order, OrderBuilder.DeclaredStandard + 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([AlertType.QuantityDelta], await OpenAlertsAsync(order));
    }

    [Fact]
    public async Task UnloadingTheMissingQuantityOfAReceivedShortShipmentResolvesItsAlert()
    {
        Order order = api.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard - 2);
        OrderBuilder.Receive(order, shipment);
        await api.SaveAsync(order);
        Assert.Equal([AlertType.QuantityDelta], await OpenAlertsAsync(order));

        using HttpResponseMessage response = await PostAsync(order.Number, OperationRequest.Unloading(order, 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await OpenAlertsAsync(order));
    }

    [Fact]
    public async Task UnloadingWithoutAPhotoOpensAMissingPhotoAlert()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Unloading(order, 4, photos: 0));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([AlertType.MissingPhoto], await OpenAlertsAsync(order));
    }

    [Fact]
    public async Task OperationIsRecordedOnTheShipmentAtTheGivenPosition()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Unloading(order, 3, position: 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Empty(detail.Shipments[0].Operations);
        Assert.Equal(3, Assert.Single(detail.Shipments[1].Operations).Quantity);
    }

    [Fact]
    public async Task DisposalWithoutATrailerIsRecordedWithoutComments()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);
        OperationRequest disposal = OperationRequest.Unloading(order, 1, photos: 2) with
        {
            Type = OperationType.Disposal,
            TrailerNumber = null,
            IsBillable = false,
        };

        using HttpResponseMessage response = await PostAsync(order.Number, disposal);
        RecordedOperationDto recorded = await ReadCreatedAsync(response);

        Assert.Equal(
            (OperationType.Disposal, (string?)null, false, 2, 0),
            (recorded.Operation.Type, recorded.Operation.TrailerNumber, recorded.Operation.IsBillable,
                recorded.Operation.PhotoCount, recorded.Operation.CommentCount));
    }

    [Fact]
    public async Task LoadingOfWhatIsOnStockEmptiesIt()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Loading(order, OrderBuilder.DeclaredStandard));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Equal(0, Assert.Single(Assert.Single(detail.Shipments).Quantities).OnStock);
    }

    [Fact]
    public async Task UnknownOrderIsNotFound()
    {
        Order neverSaved = api.Orders.Ready();

        using HttpResponseMessage response = await PostAsync("FR009999", OperationRequest.Unloading(neverSaved, 4));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "order.not_found", "Order FR009999 not found.");
    }

    [Fact]
    public async Task OrderOfAnotherCompanyIsNotFound()
    {
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Ready();
        await api.SaveAsync([.. otherCompany.Entities(), foreign]);

        using HttpResponseMessage response = await PostAsync(foreign.Number, OperationRequest.Unloading(foreign, 4));

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            $"Order {foreign.Number} not found.");
        await AssertUnchangedAsync(foreign);
    }

    [Fact]
    public async Task StaleVersionIsAConflictNamingBothVersions()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);
        OperationRequest stale = OperationRequest.Loading(order, 1) with { ExpectedVersion = order.Version - 1 };

        using HttpResponseMessage response = await PostAsync(order.Number, stale);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.version_conflict",
            $"Order {order.Number} was changed since you loaded it (version {order.Version}, expected " +
            $"{order.Version - 1}). Reload and try again.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task RepeatedRequestIsAConflictAndAddsNothing()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        OperationRequest unloading = OperationRequest.Unloading(order, 4);
        using HttpResponseMessage first = await PostAsync(order.Number, unloading);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage repeated = await PostAsync(order.Number, unloading);

        await AssertProblemAsync(
            repeated,
            HttpStatusCode.Conflict,
            "order.version_conflict",
            $"Order {order.Number} was changed since you loaded it (version {order.Version + 1}, expected " +
            $"{order.Version}). Reload and try again.");
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Equal(order.Version + 1, detail.Version);
        Assert.Single(Assert.Single(detail.Shipments).Operations);
    }

    [Fact]
    public async Task VersionIsCheckedBeforeTheShipmentPosition()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        OperationRequest staleAndMisplaced = OperationRequest.Unloading(order, 4, position: 2) with
        {
            ExpectedVersion = order.Version - 1,
        };

        using HttpResponseMessage response = await PostAsync(order.Number, staleAndMisplaced);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.version_conflict",
            $"Order {order.Number} was changed since you loaded it (version {order.Version}, expected " +
            $"{order.Version - 1}). Reload and try again.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task PositionTheOrderLacksIsNotFound()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Unloading(order, 4, position: 2));

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "shipment.not_found",
            $"Order {order.Number} has no shipment at position 2.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task DraftWithoutShipmentsHasNoShipmentAtAnyPosition()
    {
        Order draft = api.Orders.Draft();
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await PostAsync(draft.Number, OperationRequest.Unloading(draft, 4));

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "shipment.not_found",
            $"Order {draft.Number} has no shipment at position 1.");
        await AssertUnchangedAsync(draft);
    }

    [Fact]
    public async Task DraftWithTheShipmentIsNotActive()
    {
        Order draft = api.Orders.Draft();
        api.Orders.AddShipment(draft, OrderBuilder.DeclaredStandard);
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await PostAsync(draft.Number, OperationRequest.Unloading(draft, 4));

        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.not_active",
            $"Order {draft.Number} is Draft; the hub works only on a Ready or InProgress order.");
        await AssertUnchangedAsync(draft);
    }

    [Fact]
    public async Task CompletedOrderIsNotActive()
    {
        Order order = api.Orders.Completed();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(order.Number, OperationRequest.Unloading(order, 4));

        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.not_active",
            $"Order {order.Number} is Completed; the hub works only on a Ready or InProgress order.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task LoadingMoreThanIsOnStockIsRefusedAndWritesNothing()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await PostAsync(
            order.Number,
            OperationRequest.Loading(order, OrderBuilder.DeclaredStandard + 1));

        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "operation.exceeds_stock",
            $"Cannot load 11 Standard on {order.Number}-1: 10 on stock.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task QueryStringDoesNotRedirectTheWrite()
    {
        Order requested = api.Orders.Ready();
        Order other = api.Orders.Ready();
        await api.SaveAsync(requested, other);

        using HttpResponseMessage response = await api.SendPostAsync(
            $"{OperationRequest.UrlOf(requested.Number)}?number={other.Number}",
            OperationRequest.Unloading(requested, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(Assert.Single((await api.DetailAsync(requested.Number)).Shipments).Operations);
        await AssertUnchangedAsync(other);
    }

    [Fact]
    public async Task QueryStringDoesNotReplaceAValueOfTheBody()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.SendPostAsync(
            $"{OperationRequest.UrlOf(order.Number)}?quantity=9&expectedVersion=0",
            OperationRequest.Unloading(order, 4));

        RecordedOperationDto recorded = await ReadCreatedAsync(response);
        Assert.Equal(4, recorded.Operation.Quantity);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static async Task<RecordedOperationDto> ReadCreatedAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Answered {response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<RecordedOperationDto>(
            ApiJson.Options,
            TestContext.Current.CancellationToken))!;
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        string detail)
    {
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
    }

    private Task<HttpResponseMessage> PostAsync(string number, OperationRequest request)
    {
        return api.SendPostAsync(OperationRequest.UrlOf(number), request);
    }

    private async Task<AlertType[]> OpenAlertsAsync(Order order)
    {
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        return detail.Shipments
            .SelectMany(shipment => shipment.Alerts)
            .Where(alert => alert.ResolvedAt is null)
            .Select(alert => alert.Type)
            .ToArray();
    }

    // The order as it was built: the same version, Operations and Alerts.
    private async Task AssertUnchangedAsync(Order order)
    {
        Order stored = await api.InScopeAsync(services =>
            services.GetRequiredService<AppDbContext>().LoadOrderAsync(order.Id));

        Assert.Equal(order.Version, stored.Version);
        Assert.Equal(CountOperations(order), CountOperations(stored));
        Assert.Equal(CountAlerts(order), CountAlerts(stored));
    }

    private static int CountOperations(Order order)
    {
        return order.Shipments.Sum(shipment => shipment.Operations.Count);
    }

    private static int CountAlerts(Order order)
    {
        return order.Shipments.Sum(shipment => shipment.Alerts.Count);
    }
}
