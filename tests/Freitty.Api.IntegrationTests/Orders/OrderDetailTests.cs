using System.Net;
using Freitty.Application.Common;
using Freitty.Application.Orders;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Domain.Billing;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderDetailTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task DetailCarriesTheOrderHeader()
    {
        Order order = api.Orders.Draft();
        api.Orders.AddShipment(order, OrderBuilder.DeclaredStandard);
        order.AddService(api.Data.Service.Id, OrderBuilder.BuiltAt).MustSucceed();
        order.AddService(api.Data.OtherService.Id, OrderBuilder.BuiltAt).MustSucceed();
        order.ArrangeCarrier(api.Data.Carrier.Id, OrderBuilder.BuiltAt).MustSucceed();
        OrderBuilder.Submit(order, CabinetApi.Now);
        order.TransferTo(api.Data.OtherHub.Id, OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(order.Number, detail.Number);
        Assert.Equal(OrderType.CrossDock, detail.Type);
        Assert.Equal(CrossDockKind.Storage, detail.CrossDockKind);
        Assert.Equal(OrderStatus.Ready, detail.Status);
        Assert.Equal(order.Version, detail.Version);
        Assert.Equal(new UserDto("User 1", "U1", UserRole.Admin), detail.CreatedBy);
        Assert.Equal(new DetailCompanyDto(api.Data.Company.Name), detail.Company);
        Assert.Equal(new DetailHubDto(api.Data.Hub.Name, "Mark", "ON", "America/Toronto"), detail.Hub);
        Assert.Equal(new TransferHubDto(api.Data.OtherHub.Name, "Tor"), detail.TransferHub);
        Assert.Equal(CabinetApi.Now, detail.ScheduledAt);
        Assert.Equal(
            new DestinationDto(OrderBuilder.DestinationCity, OrderBuilder.DestinationRegion),
            detail.Destination);
        Assert.Equal(
            new DetailTransportDto(TransportKind.Carrier, api.Data.Carrier.Name, "+1 416 555 0100", null),
            detail.Transport);
        Assert.Equal([api.Data.OtherService.Name, api.Data.Service.Name], detail.Services);
    }

    [Fact]
    public async Task DetailSumsTheLedgerOfAShipment()
    {
        Order order = api.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, 12, photos: 4);
        OrderBuilder.Receive(order, shipment);
        OrderBuilder.DisposeOf(order, shipment, 1, photos: 2, comments: 1);
        OrderBuilder.Restack(order, shipment, 11, photos: 1);
        OrderBuilder.Load(order, shipment, 6, trailerNumber: "OUT-1");
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(
            [new ShipmentQuantityDto(UnitType.Standard, Declared: 10, Actual: 12, Delta: 2, OnStock: 5)],
            Assert.Single(detail.Shipments).Quantities);
        OrderTotalsDto totals = detail.Totals;
        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 10)], totals.Declared);
        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 12)], totals.Actual);
        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 5)], totals.OnStock);
        Assert.Equal(
            (QuantityDeltaCount: 1, PhotoCount: 7, CommentCount: 1, OutboundTrailerCount: 1),
            (totals.QuantityDeltaCount, totals.PhotoCount, totals.CommentCount, totals.OutboundTrailerCount));
        Assert.Equal(OrderStage.Loading, detail.Stage);
    }

    [Fact]
    public async Task SupplyLinesAreTotalledAndOrderedByCategoryThenSku()
    {
        var wrap = new SupplyItem(TestData.Unique("WRAP-"), "Wrap", 24.00m);
        var secondStrap = new SupplyItem(TestData.Unique("S2-"), "Securement", 18.50m);
        var firstStrap = new SupplyItem(TestData.Unique("S1-"), "Securement", 12.25m);
        Order order = api.Orders.Ready();
        order.AddSupply(wrap, 2, OrderBuilder.BuiltAt).MustSucceed();
        order.AddSupply(secondStrap, 4, OrderBuilder.BuiltAt).MustSucceed();
        order.AddSupply(firstStrap, 3, OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(wrap, secondStrap, firstStrap, order);

        SuppliesDto supplies = (await api.DetailAsync(order.Number)).Supplies;

        Assert.Equal(
            [
                new SupplyLineDto(firstStrap.Sku, "Securement", 3, 12.25m, 36.75m),
                new SupplyLineDto(secondStrap.Sku, "Securement", 4, 18.50m, 74.00m),
                new SupplyLineDto(wrap.Sku, "Wrap", 2, 24.00m, 48.00m),
            ],
            supplies.Lines);
        Assert.Equal(158.75m, supplies.Subtotal);
    }

    [Fact]
    public async Task ShortageHasNoDeltaBeforeTheShipmentIsReceivedAndANegativeOneAfter()
    {
        Order stillArriving = api.Orders.Ready();
        OrderBuilder.Unload(stillArriving, stillArriving.Shipments.Single(), OrderBuilder.DeclaredStandard - 2);
        Order received = api.Orders.Ready();
        OrderBuilder.Unload(received, received.Shipments.Single(), OrderBuilder.DeclaredStandard - 2);
        OrderBuilder.Receive(received, received.Shipments.Single());
        await api.SaveAsync(stillArriving, received);

        OrderDetailDto stillArrivingDetail = await api.DetailAsync(stillArriving.Number);
        OrderDetailDto receivedDetail = await api.DetailAsync(received.Number);

        Assert.Null(Assert.Single(Assert.Single(stillArrivingDetail.Shipments).Quantities).Delta);
        Assert.Equal(0, stillArrivingDetail.Totals.QuantityDeltaCount);
        Assert.Equal(-2, Assert.Single(Assert.Single(receivedDetail.Shipments).Quantities).Delta);
        Assert.Equal(1, receivedDetail.Totals.QuantityDeltaCount);
    }

    [Fact]
    public async Task UnitTypeUnloadedButNotDeclaredHasItsOwnRowWithNothingDeclared()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), 2, UnitType.ExtraLarge);
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(
            [
                new ShipmentQuantityDto(UnitType.Standard, Declared: 10, Actual: 0, Delta: null, OnStock: 0),
                new ShipmentQuantityDto(UnitType.ExtraLarge, Declared: 0, Actual: 2, Delta: 2, OnStock: 2),
            ],
            Assert.Single(detail.Shipments).Quantities);
        Assert.Equal(
            [new UnitQuantityDto(UnitType.Standard, 10), new UnitQuantityDto(UnitType.ExtraLarge, 0)],
            detail.Totals.Declared);
        Assert.Equal(
            [new UnitQuantityDto(UnitType.Standard, 0), new UnitQuantityDto(UnitType.ExtraLarge, 2)],
            detail.Totals.Actual);
    }

    [Fact]
    public async Task ConsolidationReturnsEachShipmentWithItsOwnState()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        Shipment[] shipments = order.Shipments.ToArray();
        OrderBuilder.Arrive(order, shipments[0]);
        OrderBuilder.AssignDock(order, shipments[0], "12");
        OrderBuilder.Unload(order, shipments[0], OrderBuilder.DeclaredStandard);
        order.SetWarehouseNote(shipments[0].Id, "Counted on arrival.", OrderBuilder.BuiltAt).MustSucceed();
        OrderBuilder.Receive(order, shipments[0]);
        OrderBuilder.DisposeOf(order, shipments[0], 1, photos: 2, comments: 1);
        OrderBuilder.Unload(order, shipments[1], OrderBuilder.DeclaredStandard + 1, photos: 0);
        OrderBuilder.Restack(order, shipments[1], OrderBuilder.DeclaredStandard + 1, photos: 1);
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(OrderStage.Unloading, detail.Stage);
        DetailShipmentDto first = detail.Shipments[0];
        Assert.Equal((1, $"{order.Number}-1", shipments[0].RefNumber), (first.Position, first.Label, first.RefNumber));
        Assert.Equal(
            new ArrivalDto(
                TrailerType.DryVan53,
                OrderBuilder.TruckNumber,
                OrderBuilder.TrailerNumber,
                OrderBuilder.BuiltAt),
            first.Arrival);
        Assert.Equal(new DockDto("12", OrderBuilder.Bay, OrderBuilder.FloorLead, OrderBuilder.BuiltAt), first.Dock);
        Assert.Equal(OrderBuilder.BuiltAt, first.ReceivedAt);
        Assert.Equal("Counted on arrival.", first.WarehouseNote);
        Assert.Empty(first.Alerts);
        Assert.Equal(
            [OperationType.Unloading, OperationType.Disposal],
            first.Operations.Select(operation => operation.Type).Order());

        DetailShipmentDto second = detail.Shipments[1];
        Assert.Equal((2, $"{order.Number}-2"), (second.Position, second.Label));
        Assert.Null(second.Arrival);
        Assert.Null(second.Dock);
        Assert.Null(second.ReceivedAt);
        Assert.Equal(
            [(AlertType.QuantityDelta, (DateTimeOffset?)null), (AlertType.MissingPhoto, null)],
            second.Alerts.Select(alert => (alert.Type, alert.ResolvedAt)));
        Assert.Equal(
            [new ShipmentQuantityDto(UnitType.Standard, Declared: 10, Actual: 11, Delta: 1, OnStock: 11)],
            second.Quantities);
    }

    [Fact]
    public async Task TotalsAddUpEveryShipmentOfAConsolidation()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        Shipment[] shipments = order.Shipments.ToArray();
        OrderBuilder.Unload(order, shipments[0], OrderBuilder.DeclaredStandard, photos: 2);
        OrderBuilder.Receive(order, shipments[0]);
        OrderBuilder.DisposeOf(order, shipments[0], 1, photos: 1, comments: 1);
        OrderBuilder.Unload(order, shipments[1], OrderBuilder.DeclaredStandard + 1, photos: 3);
        OrderBuilder.Restack(order, shipments[1], 4, photos: 1);
        await api.SaveAsync(order);

        OrderTotalsDto totals = (await api.DetailAsync(order.Number)).Totals;

        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 20)], totals.Declared);
        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 21)], totals.Actual);
        Assert.Equal([new UnitQuantityDto(UnitType.Standard, 20)], totals.OnStock);
        Assert.Equal(
            (QuantityDeltaCount: 1, PhotoCount: 7, CommentCount: 1),
            (totals.QuantityDeltaCount, totals.PhotoCount, totals.CommentCount));
    }

    [Fact]
    public async Task OutboundTrailersAreTheDistinctTrailersOfLoadings()
    {
        Order order = api.Orders.Received(OrderType.Consolidation);
        Shipment[] shipments = order.Shipments.ToArray();
        OrderBuilder.Load(order, shipments[0], 4, trailerNumber: "OUT-1");
        OrderBuilder.Load(order, shipments[1], 4, trailerNumber: "OUT-1");
        OrderBuilder.Load(order, shipments[1], 4, trailerNumber: "OUT-2");
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(2, detail.Totals.OutboundTrailerCount);
    }

    [Fact]
    public async Task OwnDriverTransportNamesTheDriverAndNoCarrier()
    {
        Order order = api.Orders.Draft();
        order.ArrangeOwnDriver("User 5", OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(new DetailTransportDto(TransportKind.OwnDriver, null, null, "User 5"), detail.Transport);
    }

    [Fact]
    public async Task OperationsComeBackInRecordedOrder()
    {
        Order order = api.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard, at: At(minutes: 1));
        OrderBuilder.Receive(order, shipment, At(minutes: 2));
        OrderBuilder.DisposeOf(order, shipment, 1, at: At(minutes: 3));
        OrderBuilder.Restack(order, shipment, 9, at: At(minutes: 4));
        OrderBuilder.Load(order, shipment, 9, at: At(minutes: 5));
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(
            [
                (OperationType.Unloading, At(minutes: 1)),
                (OperationType.Disposal, At(minutes: 3)),
                (OperationType.Restack, At(minutes: 4)),
                (OperationType.Loading, At(minutes: 5)),
            ],
            Assert.Single(detail.Shipments).Operations.Select(operation => (operation.Type, operation.AppliedAt)));
    }

    [Fact]
    public async Task OpenAndResolvedAlertsBothComeBackInTheOrderTheyWereRaised()
    {
        Order order = api.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard - 1, photos: 0, at: At(minutes: 1));
        OrderBuilder.Receive(order, shipment, At(minutes: 2));
        Alert missingPhoto = shipment.Alerts.Single(alert => alert.Type == AlertType.MissingPhoto);
        order.ResolveAlert(missingPhoto.Id, At(minutes: 3)).MustSucceed();
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(
            [
                (AlertType.MissingPhoto, At(minutes: 1), At(minutes: 3)),
                (AlertType.QuantityDelta, At(minutes: 2), (DateTimeOffset?)null),
            ],
            Assert.Single(detail.Shipments).Alerts.Select(alert => (alert.Type, alert.RaisedAt, alert.ResolvedAt)));
    }

    [Theory]
    [InlineData(OrderStage.Draft)]
    [InlineData(OrderStage.AwaitingTruck)]
    [InlineData(OrderStage.Unloading)]
    [InlineData(OrderStage.Loading)]
    [InlineData(OrderStage.AwaitingCompletion)]
    [InlineData(OrderStage.Completed)]
    public async Task StageDescribesWhereTheFreightIs(OrderStage stage)
    {
        Order order = OrderAt(stage);
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(stage, detail.Stage);
    }

    [Fact]
    public async Task InvoicesComeBackInIssueOrderWithTheirPayment()
    {
        Order order = api.Orders.Completed();
        Invoice unpaid = OrderBuilder.IssueInvoice(order, 1540.00m, OrderBuilder.BuiltAt.AddHours(2), paid: false);
        Invoice paid = OrderBuilder.IssueInvoice(order, 280.00m, OrderBuilder.BuiltAt.AddHours(1), paid: true);
        await api.SaveAsync(order, unpaid, paid);

        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal(
            [
                new InvoiceDto(
                    paid.Number,
                    280.00m,
                    OrderBuilder.BuiltAt.AddHours(1),
                    OrderBuilder.BuiltAt.AddHours(1)),
                new InvoiceDto(unpaid.Number, 1540.00m, OrderBuilder.BuiltAt.AddHours(2), null),
            ],
            detail.Invoices);
    }

    [Fact]
    public async Task DraftWithoutShipmentsIsReturnedWithEmptyCollections()
    {
        Order draft = api.Orders.Draft(OrderType.Consolidation);
        await api.SaveAsync(draft);

        OrderDetailDto detail = await api.DetailAsync(draft.Number);

        Assert.Equal(OrderStage.Draft, detail.Stage);
        Assert.Null(detail.CrossDockKind);
        Assert.Null(detail.ScheduledAt);
        Assert.Null(detail.Destination);
        Assert.Null(detail.Transport);
        Assert.Empty(detail.Services);
        Assert.Empty(detail.Shipments);
        Assert.Empty(detail.Totals.Declared);
        Assert.Empty(detail.Totals.Actual);
        Assert.Empty(detail.Totals.OnStock);
        OrderTotalsDto totals = detail.Totals;
        Assert.Equal(
            (QuantityDeltaCount: 0, PhotoCount: 0, CommentCount: 0, OutboundTrailerCount: 0),
            (totals.QuantityDeltaCount, totals.PhotoCount, totals.CommentCount, totals.OutboundTrailerCount));
        Assert.Empty(detail.Supplies.Lines);
        Assert.Equal(0m, detail.Supplies.Subtotal);
        Assert.Empty(detail.Invoices);
    }

    [Fact]
    public async Task NumberIsMatchedCaseInsensitively()
    {
        Order order = api.Orders.Draft(draftNumber: TestData.Unique("Draft-Mixed-"));
        await api.SaveAsync(order);

        OrderDetailDto lowerCased = await api.DetailAsync(order.Number.ToLowerInvariant());
        OrderDetailDto upperCased = await api.DetailAsync(order.Number.ToUpperInvariant());

        Assert.Equal(order.Number, lowerCased.Number);
        Assert.Equal(order.Number, upperCased.Number);
    }

    [Fact]
    public async Task QueryStringDoesNotReplaceTheNumberOfTheRoute()
    {
        Order requested = api.Orders.Ready();
        Order other = api.Orders.Ready();
        await api.SaveAsync(requested, other);

        OrderDetailDto detail = await api.GetAsync<OrderDetailDto>(
            $"{ApiUrls.Order(requested.Number)}?number={other.Number}");

        Assert.Equal(requested.Number, detail.Number);
    }

    [Fact]
    public async Task UnknownNumberIsANotFoundProblemNamingTheNumber()
    {
        await AssertNotFoundAsync("fr009999");
    }

    [Fact]
    public async Task OrderOfAnotherCompanyIsNotFound()
    {
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Ready();
        await api.SaveAsync([.. otherCompany.Entities(), foreign]);

        await AssertNotFoundAsync(foreign.Number);
    }

    [Fact]
    public async Task UnderscoreInTheNumberIsNoWildcard()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        await AssertNotFoundAsync(order.Number[..^1] + "_");
    }

    [Fact]
    public async Task QuantitiesAndSubtotalEqualTheComputedMembersOfTheLoadedAggregate()
    {
        Order order = api.Orders.Draft(OrderType.Consolidation);
        Shipment first = api.Orders.AddShipment(order, standard: 10, extraLarge: 3);
        Shipment second = api.Orders.AddShipment(order, standard: 8);
        order.ArrangeSelfPickup(OrderBuilder.BuiltAt).MustSucceed();
        OrderBuilder.Submit(order, CabinetApi.Now);
        OrderBuilder.Unload(order, first, 11);
        OrderBuilder.Unload(order, first, 2, UnitType.ExtraLarge, photos: 0);
        OrderBuilder.Receive(order, first);
        OrderBuilder.DisposeOf(order, first, 1);
        OrderBuilder.Load(order, first, 4);
        OrderBuilder.Unload(order, second, 6);
        order.AddSupply(api.Data.SupplyItem, 3, OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(order);

        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Order loaded = await api.InContextAsync(db => db.LoadOrderAsync(order.Id));

        Assert.Equal(loaded.Shipments.Count, detail.Shipments.Count);
        foreach (Shipment shipment in loaded.Shipments)
        {
            DetailShipmentDto shipmentDetail = detail.Shipments.Single(row => row.Position == shipment.Position);
            UnitType[] unitTypes = shipment.CargoLines.Select(line => line.UnitType)
                .Concat(shipment.Operations.Select(operation => operation.UnitType))
                .Distinct()
                .Order()
                .ToArray();
            Assert.Equal(
                unitTypes.Select(unitType => (unitType, shipment.DeclaredQuantity(unitType),
                    shipment.ActualQuantity(unitType), shipment.OnStock(unitType))),
                shipmentDetail.Quantities.Select(row => (row.UnitType, row.Declared, row.Actual, row.OnStock)));
            Assert.Equal(
                shipment.HasQuantityDelta,
                shipmentDetail.Quantities.Any(row => row.Delta is not (null or 0)));
        }

        Assert.Equal(loaded.SupplySubtotal, detail.Supplies.Subtotal);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static DateTimeOffset At(int minutes)
    {
        return OrderBuilder.BuiltAt.AddMinutes(minutes);
    }

    private Order OrderAt(OrderStage stage)
    {
        return stage switch
        {
            OrderStage.Draft => api.Orders.Draft(),
            OrderStage.AwaitingTruck => api.Orders.Ready(),
            OrderStage.Unloading => Arrived(api.Orders.Ready()),
            OrderStage.Loading => api.Orders.Received(),
            OrderStage.AwaitingCompletion => LoadedOut(api.Orders.Received()),
            OrderStage.Completed => api.Orders.Completed(),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "No order is built for this stage."),
        };
    }

    private static Order Arrived(Order order)
    {
        OrderBuilder.Arrive(order, order.Shipments.Single());
        return order;
    }

    private static Order LoadedOut(Order order)
    {
        OrderBuilder.Load(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard);
        return order;
    }

    private async Task AssertNotFoundAsync(string number)
    {
        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Order(number));

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            $"Order {number} not found.");
    }
}
