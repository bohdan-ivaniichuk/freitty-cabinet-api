using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Common;
using Freitty.Application.Orders.Cards;
using Freitty.Domain.Billing;
using Freitty.Domain.Companies;
using Freitty.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderCardTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task CardCarriesTheOrderHeader()
    {
        Order order = api.Orders.Ready(scheduledAt: CabinetApi.Now);
        TestData.Succeed(order.TransferTo(api.Data.OtherHub.Id, TestData.Now));
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal(OrderType.CrossDock, card.Type);
        Assert.Equal(CrossDockKind.Storage, card.CrossDockKind);
        Assert.Equal(OrderStatus.Ready, card.Status);
        Assert.Equal(new UserDto("User 1", "U1", UserRole.Admin), card.CreatedBy);
        Assert.Equal(new OrderHubDto(api.Data.Hub.Name, "Mark", "America/Toronto"), card.Hub);
        Assert.Equal(new TransferHubDto(api.Data.OtherHub.Name, "Tor"), card.TransferHub);
        Assert.Equal(CabinetApi.Now, card.ScheduledAt);
        Assert.Equal(new DestinationDto("Montreal", "QC"), card.Destination);
        Assert.Equal(new TransportDto(TransportKind.Carrier, api.Data.Carrier.Name, null), card.Transport);
    }

    [Fact]
    public async Task DraftCardHasNoValuesItHasNotSet()
    {
        Order draft = api.Orders.Draft(OrderType.Consolidation);
        await api.SaveAsync(draft);

        OrderCardDto card = await CardAsync(draft);

        Assert.Null(card.CrossDockKind);
        Assert.Null(card.ScheduledAt);
        Assert.Null(card.Destination);
        Assert.Null(card.Transport);
        Assert.Null(card.TransferHub);
        Assert.Empty(card.Declared);
        Assert.Empty(card.Shipments);
    }

    [Fact]
    public async Task OwnDriverTransportNamesTheDriver()
    {
        Order order = api.Orders.Draft();
        TestData.Succeed(order.ArrangeOwnDriver("User 5", TestData.Now));
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal(new TransportDto(TransportKind.OwnDriver, null, "User 5"), card.Transport);
    }

    [Fact]
    public async Task CardSumsTheLedgerPerUnitTypeAndPerShipment()
    {
        Order order = api.Orders.Draft(OrderType.Consolidation);
        Shipment first = api.Orders.AddShipment(order, standard: 10, extraLarge: 3, refNumber: "REF-A");
        Shipment second = api.Orders.AddShipment(order, standard: 8, refNumber: "REF-B");
        TestData.Succeed(order.ArrangeSelfPickup(TestData.Now));
        OrderBuilder.Submit(order, CabinetApi.Now);
        OrderBuilder.Unload(order, first, 10);
        OrderBuilder.Unload(order, first, 3, UnitType.ExtraLarge);
        OrderBuilder.Unload(order, second, 8);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal(
            [new UnitQuantityDto(UnitType.Standard, 18), new UnitQuantityDto(UnitType.ExtraLarge, 3)],
            card.Declared);
        Assert.Equal(21, card.DeclaredTotal);
        Assert.Equal(21, card.ActualTotal);
        Assert.False(card.HasQuantityDelta);
        Assert.Equal(
            [($"{order.Number}-1", "REF-A", 13, 13), ($"{order.Number}-2", "REF-B", 8, 8)],
            card.Shipments.Select(shipment =>
                (shipment.Label, shipment.RefNumber, shipment.DeclaredQuantity, shipment.ActualQuantity)));
    }

    [Fact]
    public async Task UnitTypeUnloadedButNeverDeclaredCountsAsActualButIsNoDeclaredLine()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), 2, UnitType.ExtraLarge);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal([new UnitQuantityDto(UnitType.Standard, OrderBuilder.DeclaredStandard)], card.Declared);
        Assert.Equal(2, card.ActualTotal);
        Assert.True(card.HasQuantityDelta);
    }

    [Fact]
    public async Task ReaderGivesNoCardForAnOrderOfAnotherCompany()
    {
        Order own = api.Orders.Ready();
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Ready();
        await api.SaveAsync([.. otherCompany.Entities(), own, foreign]);

        IReadOnlyList<OrderCardDto> cards = await api.InScopeAsync(services => services
            .GetRequiredService<OrderCardReader>()
            .ReadAsync(api.Data.Company.Id, [foreign.Id, own.Id], TestContext.Current.CancellationToken));

        Assert.Equal(own.Number, Assert.Single(cards).Number);
    }

    [Fact]
    public async Task ExcessIsAQuantityDeltaAsSoonAsItIsUnloaded()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard + 2);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.True(card.HasQuantityDelta);
        Assert.Equal(OrderBuilder.DeclaredStandard + 2, card.ActualTotal);
    }

    [Fact]
    public async Task ShortageIsAQuantityDeltaOnlyOnceTheShipmentIsReceived()
    {
        Order stillArriving = api.Orders.Ready();
        OrderBuilder.Unload(stillArriving, stillArriving.Shipments.Single(), OrderBuilder.DeclaredStandard - 2);
        Order received = api.Orders.Ready();
        OrderBuilder.Unload(received, received.Shipments.Single(), OrderBuilder.DeclaredStandard - 2);
        OrderBuilder.Receive(received, received.Shipments.Single());
        await api.SaveAsync(stillArriving, received);

        OrderCardDto stillArrivingCard = await CardAsync(stillArriving);
        OrderCardDto receivedCard = await CardAsync(received);

        Assert.False(stillArrivingCard.HasQuantityDelta);
        Assert.True(receivedCard.HasQuantityDelta);
    }

    [Fact]
    public async Task ActualIsWhatWasUnloadedNotWhatIsLeftOnStock()
    {
        Order order = api.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard);
        OrderBuilder.Receive(order, shipment);
        OrderBuilder.DisposeOf(order, shipment, 2);
        OrderBuilder.Load(order, shipment, OrderBuilder.DeclaredStandard - 2);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal(OrderBuilder.DeclaredStandard, card.ActualTotal);
        Assert.False(card.HasQuantityDelta);
        Assert.Equal(NextStepCode.AwaitingCompletion, card.Next.Code);
    }

    [Fact]
    public async Task OutboundTrailersAreTheDistinctTrailersOfLoadings()
    {
        Order order = api.Orders.Received(OrderType.Consolidation);
        Shipment[] shipments = order.Shipments.ToArray();
        OrderBuilder.Load(order, shipments[0], 4, trailerNumber: "OUT-1");
        OrderBuilder.Load(order, shipments[0], 4, trailerNumber: "OUT-1");
        OrderBuilder.Load(order, shipments[1], 4, trailerNumber: "OUT-2");
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Equal(2, card.OutboundTrailers);
    }

    [Fact]
    public async Task OpenAlertTypesAreListedPerShipmentAndForTheOrderInEnumOrder()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        Shipment[] shipments = order.Shipments.ToArray();
        OrderBuilder.Unload(order, shipments[1], OrderBuilder.DeclaredStandard + 1, photos: 0);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        AlertType[] bothTypes = [AlertType.QuantityDelta, AlertType.MissingPhoto];
        Assert.Equal(bothTypes, card.OpenAlertTypes);
        Assert.Empty(card.Shipments[0].OpenAlertTypes);
        Assert.Equal(bothTypes, card.Shipments[1].OpenAlertTypes);
    }

    [Fact]
    public async Task ResolvedAlertIsNotListed()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        OrderBuilder.ResolveAlerts(order);
        await api.SaveAsync(order);

        OrderCardDto card = await CardAsync(order);

        Assert.Empty(card.OpenAlertTypes);
    }

    [Fact]
    public async Task DraftNextStepIsToContinueEditing()
    {
        await AssertNextStepAsync(api.Orders.Draft(), NextStepCode.ContinueEditing);
    }

    [Fact]
    public async Task OpenMissingPhotoAlertComesBeforeOpenQuantityDelta()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard + 1, photos: 0);

        await AssertNextStepAsync(order, NextStepCode.UploadPhoto);
    }

    [Fact]
    public async Task OpenQuantityDeltaAlertAsksForReview()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard + 1);

        await AssertNextStepAsync(order, NextStepCode.ReviewQuantityDelta);
    }

    [Fact]
    public async Task ReadyOrderAwaitsTheTruck()
    {
        await AssertNextStepAsync(api.Orders.Ready(), NextStepCode.AwaitingTruck);
    }

    [Fact]
    public async Task ArrivedShipmentIsUnloading()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Arrive(order, order.Shipments.Single());

        await AssertNextStepAsync(order, NextStepCode.Unloading);
    }

    [Fact]
    public async Task OrderIsUnloadingWhileSomeShipmentIsNotReceived()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        Shipment first = order.Shipments.First();
        OrderBuilder.Unload(order, first, OrderBuilder.DeclaredStandard);
        OrderBuilder.Receive(order, first);

        await AssertNextStepAsync(order, NextStepCode.Unloading);
    }

    [Fact]
    public async Task ReceivedOrderWithStockIsLoading()
    {
        await AssertNextStepAsync(api.Orders.Received(), NextStepCode.Loading);
    }

    [Fact]
    public async Task ReceivedOrderWithNothingOnStockAwaitsCompletion()
    {
        Order order = api.Orders.Received();
        OrderBuilder.Load(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard);

        await AssertNextStepAsync(order, NextStepCode.AwaitingCompletion);
    }

    [Fact]
    public async Task ShipmentFinishedWithoutArrivingAwaitsCompletionNotTheTruck()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Receive(order, order.Shipments.Single());
        OrderBuilder.ResolveAlerts(order);

        await AssertNextStepAsync(order, NextStepCode.AwaitingCompletion);
    }

    [Fact]
    public async Task CompletedOrderWithUnpaidInvoiceAwaitsPaymentOfTheFirstUnpaid()
    {
        Order order = api.Orders.Completed();
        Invoice paid = OrderBuilder.IssueInvoice(order, 100.00m, TestData.Now.AddHours(1), paid: true);
        Invoice laterUnpaid = OrderBuilder.IssueInvoice(order, 300.00m, TestData.Now.AddHours(3), paid: false);
        Invoice firstUnpaid = OrderBuilder.IssueInvoice(order, 280.00m, TestData.Now.AddHours(2), paid: false);

        NextStepDto next = await SaveAndReadNextStepAsync(order, paid, laterUnpaid, firstUnpaid);

        Assert.Equal(new NextStepDto(NextStepCode.AwaitingPayment, firstUnpaid.Number, 280.00m), next);
    }

    [Fact]
    public async Task CompletedOrderWithEveryInvoicePaidShowsTheLastInvoice()
    {
        Order order = api.Orders.Completed();
        Invoice last = OrderBuilder.IssueInvoice(order, 1540.00m, TestData.Now.AddHours(2), paid: true);
        Invoice earlier = OrderBuilder.IssueInvoice(order, 100.00m, TestData.Now.AddHours(1), paid: true);

        NextStepDto next = await SaveAndReadNextStepAsync(order, last, earlier);

        Assert.Equal(new NextStepDto(NextStepCode.Paid, last.Number, 1540.00m), next);
    }

    [Fact]
    public async Task CompletedOrderWithoutInvoiceIsClosed()
    {
        await AssertNextStepAsync(api.Orders.Completed(), NextStepCode.Closed);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private async Task<OrderCardDto> CardAsync(Order order)
    {
        OrderCardDto[] cards = [.. (await api.ListAsync("?pageSize=50")).Items];
        return Assert.Single(cards, card => card.Number == order.Number);
    }

    private async Task AssertNextStepAsync(Order order, NextStepCode expectedCode)
    {
        NextStepDto next = await SaveAndReadNextStepAsync(order);

        Assert.Equal(new NextStepDto(expectedCode, null, null), next);
    }

    private async Task<NextStepDto> SaveAndReadNextStepAsync(Order order, params Invoice[] invoices)
    {
        await api.SaveAsync([order, .. invoices]);
        return (await CardAsync(order)).Next;
    }
}
