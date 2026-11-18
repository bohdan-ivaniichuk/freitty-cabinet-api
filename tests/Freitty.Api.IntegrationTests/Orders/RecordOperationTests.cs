using System.Net;
using Freitty.Application.Orders;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.RecordOperation;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class RecordOperationTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task FirstOperationStartsAReadyOrder()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        RecordedOperationDto recorded = await RecordCreatedAsync(order.Number, OperationRequest.Unloading(order, 4));

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
                PhotoCount: 0,
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

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number.ToLowerInvariant(),
            OperationRequest.Unloading(order, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ApiUrls.Order(order.Number), response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnloadingAboveTheDeclaredQuantityOpensAQuantityDeltaAlert()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number,
            OperationRequest.Unloading(order, OrderBuilder.DeclaredStandard + 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(AlertType.QuantityDelta, await OpenAlertsAsync(order));
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

        using HttpResponseMessage response = await api.RecordAsync(order.Number, OperationRequest.Unloading(order, 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain(AlertType.QuantityDelta, await OpenAlertsAsync(order));
    }

    [Fact]
    public async Task UnloadingWithoutAPhotoOpensAMissingPhotoAlert()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number,
            OperationRequest.Unloading(order, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([AlertType.MissingPhoto], await OpenAlertsAsync(order));
    }

    [Fact]
    public async Task OperationIsRecordedOnTheShipmentAtTheGivenPosition()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number,
            OperationRequest.Unloading(order, 3, position: 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Empty(detail.Shipments[0].Operations);
        Assert.Equal(3, Assert.Single(detail.Shipments[1].Operations).Quantity);
    }

    [Fact]
    public async Task DisposalWithoutATrailerIsRecordedWithoutCommentsOrPhotos()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);
        OperationRequest disposal = OperationRequest.Unloading(order, 1) with
        {
            Type = OperationType.Disposal,
            TrailerNumber = null,
            IsBillable = false,
        };

        RecordedOperationDto recorded = await RecordCreatedAsync(order.Number, disposal);

        Assert.Equal(
            (OperationType.Disposal, (string?)null, false, 0, 0),
            (recorded.Operation.Type, recorded.Operation.TrailerNumber, recorded.Operation.IsBillable,
                recorded.Operation.PhotoCount, recorded.Operation.CommentCount));
    }

    [Fact]
    public async Task LoadingOfWhatIsOnStockEmptiesIt()
    {
        Order order = api.Orders.Received();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.RecordAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(
            "FR009999",
            OperationRequest.Unloading(neverSaved, 4));

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            "Order FR009999 not found.");
    }

    [Fact]
    public async Task OrderOfAnotherCompanyIsNotFound()
    {
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Ready();
        await api.SaveAsync([.. otherCompany.Entities(), foreign]);

        using HttpResponseMessage response = await api.RecordAsync(
            foreign.Number,
            OperationRequest.Unloading(foreign, 4));

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(order.Number, stale);

        await ProblemResponse.AssertDomainProblemAsync(
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
        using HttpResponseMessage first = await api.RecordAsync(order.Number, unloading);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage repeated = await api.RecordAsync(order.Number, unloading);

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(order.Number, staleAndMisplaced);

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number,
            OperationRequest.Unloading(order, 4, position: 2));

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(draft.Number, OperationRequest.Unloading(draft, 4));

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(draft.Number, OperationRequest.Unloading(draft, 4));

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(order.Number, OperationRequest.Unloading(order, 4));

        await ProblemResponse.AssertDomainProblemAsync(
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

        using HttpResponseMessage response = await api.RecordAsync(
            order.Number,
            OperationRequest.Loading(order, OrderBuilder.DeclaredStandard + 1));

        await ProblemResponse.AssertDomainProblemAsync(
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
            $"{ApiUrls.Operations(requested.Number)}?number={other.Number}",
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

        RecordedOperationDto recorded = await api.PostAsync<RecordedOperationDto>(
            $"{ApiUrls.Operations(order.Number)}?quantity=9&expectedVersion=0",
            OperationRequest.Unloading(order, 4),
            HttpStatusCode.Created);

        Assert.Equal(4, recorded.Operation.Quantity);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private Task<RecordedOperationDto> RecordCreatedAsync(string orderNumber, OperationRequest request)
    {
        return api.PostAsync<RecordedOperationDto>(
            ApiUrls.Operations(orderNumber),
            request,
            HttpStatusCode.Created);
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
        Order stored = await api.InContextAsync(db => db.LoadOrderAsync(order.Id));

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
