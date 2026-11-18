using Freitty.Domain.Billing;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Seed;

[Collection(SeededDatabaseCollection.Name)]
public sealed class ScriptedOrderTests(SeededDatabase seeded)
{
    [Fact]
    public async Task FR001383IsBeingLoadedWithOpenQuantityDeltaAndBillsOnlyTrailerWork()
    {
        Order order = await seeded.LoadOrderAsync("FR001383");

        Shipment shipment = Assert.Single(order.Shipments);
        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equal(
            [
                (OperationType.Unloading, true),
                (OperationType.Disposal, false),
                (OperationType.Restack, false),
                (OperationType.Loading, true),
            ],
            shipment.Operations
                .OrderBy(operation => operation.AppliedAt)
                .Select(operation => (operation.Type, operation.IsBillable)));
        Assert.Equal(10, shipment.DeclaredQuantity(UnitType.Standard));
        Assert.Equal(12, shipment.ActualQuantity(UnitType.Standard));
        Assert.Equal(5, shipment.OnStock(UnitType.Standard));
        Assert.Equal(AlertType.QuantityDelta, Assert.Single(shipment.Alerts, alert => alert.ResolvedAt is null).Type);
        Assert.Equal(2, order.Services.Count);
        Assert.Equal(314.00m, order.SupplySubtotal);
    }

    [Fact]
    public async Task FR001674HasBothAlertsOpenOnItsSecondShipment()
    {
        Order order = await seeded.LoadOrderAsync("FR001674");

        Shipment second = order.Shipments.Single(shipment => shipment.Position == 2);
        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equivalent(
            new[] { AlertType.MissingPhoto, AlertType.QuantityDelta },
            second.Alerts.Where(alert => alert.ResolvedAt is null).Select(alert => alert.Type),
            strict: true);
        Assert.Empty(order.Shipments.Single(shipment => shipment.Position == 1).Alerts);
        Assert.Equal(20, order.Shipments.Sum(shipment => shipment.DeclaredQuantity(UnitType.Standard)));
        Assert.Equal(18, order.Shipments.Sum(shipment => shipment.ActualQuantity(UnitType.Standard)));
        Assert.Equal(7, order.Shipments.Sum(shipment => shipment.OnStock(UnitType.Standard)));
        Assert.Single(OutboundTrailersOf(order));
    }

    [Fact]
    public async Task FR001676HasTwoStandardOnStockAndTwoOutboundTrailers()
    {
        Order order = await seeded.LoadOrderAsync("FR001676");

        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equal(15, order.Shipments.Sum(shipment => shipment.DeclaredQuantity(UnitType.Standard)));
        Assert.Equal(3, order.Shipments.Sum(shipment => shipment.DeclaredQuantity(UnitType.ExtraLarge)));
        Assert.Equal(2, order.Shipments.Sum(shipment => shipment.OnStock(UnitType.Standard)));
        Assert.Equal(0, order.Shipments.Sum(shipment => shipment.OnStock(UnitType.ExtraLarge)));
        Assert.Equal(2, OutboundTrailersOf(order).Count());
        Assert.False(order.HasOpenAlerts);
    }

    [Fact]
    public async Task FR001677IsInProgressWithEverythingOnStock()
    {
        Order order = await seeded.LoadOrderAsync("FR001677");

        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equal(14, Assert.Single(order.Shipments).OnStock(UnitType.Standard));
        Assert.False(order.HasOpenAlerts);
    }

    [Fact]
    public async Task FR001678HasArrivedAndNothingIsUnloaded()
    {
        Order order = await seeded.LoadOrderAsync("FR001678");

        Shipment shipment = Assert.Single(order.Shipments);
        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.NotNull(shipment.ArrivedAt);
        Assert.Empty(shipment.Operations);
    }

    [Fact]
    public async Task FR001679IsReadyForTheSeedingDay()
    {
        Order order = await seeded.LoadOrderAsync("FR001679");

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Equal(new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero), order.ScheduledAt);
    }

    [Fact]
    public async Task FR001681IsReadyAndScheduledAfterNow()
    {
        Order order = await seeded.LoadOrderAsync("FR001681");

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.True(order.ScheduledAt > SeededDatabase.Now);
    }

    [Fact]
    public async Task DRAFT003HasNoShipmentDestinationOrTransport()
    {
        Order order = await seeded.LoadOrderAsync("DRAFT-003");

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(order.Shipments);
        Assert.Null(order.DestinationCity);
        Assert.Null(order.TransportKind);
    }

    [Fact]
    public async Task DRAFT004HasEverythingSubmitNeedsAndIsNotSubmitted()
    {
        Order order = await seeded.LoadOrderAsync("DRAFT-004");

        Assert.Equal((OrderStatus.Draft, OrderType.CrossDock), (order.Status, order.Type));
        Assert.Equal(12, Assert.Single(order.Shipments).DeclaredQuantity(UnitType.Standard));
        Assert.Equal(("London", TransportKind.Carrier), (order.DestinationCity, order.TransportKind));
        Assert.True(order.ScheduledAt > SeededDatabase.Now);
        Assert.Null(order.SubmittedAt);
    }

    [Fact]
    public async Task FR001672HasPaidInvoiceOf280()
    {
        Invoice invoice = await LoadInvoiceAsync("FR001672");

        Assert.Equal(OrderStatus.Completed, invoice.Order.Status);
        Assert.Equal("001812", invoice.Number);
        Assert.Equal(280.00m, invoice.Amount);
        Assert.NotNull(invoice.PaidAt);
    }

    [Fact]
    public async Task FR001668IsTransferredToTorontoWithUnpaidInvoiceOf1540()
    {
        Order order = await seeded.LoadOrderAsync("FR001668");
        Invoice invoice = await LoadInvoiceAsync("FR001668");
        string? transferHubName = await seeded.InContextAsync(db => db.Hubs
            .Where(hub => hub.Id == order.TransferHubId)
            .Select(hub => hub.Name)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(55, order.Shipments.Sum(shipment => shipment.DeclaredQuantity(UnitType.Standard)));
        Assert.Equal(55, order.Shipments.Sum(shipment => shipment.ActualQuantity(UnitType.Standard)));
        Assert.Equal("Toronto", transferHubName);
        Assert.Equal("001809", invoice.Number);
        Assert.Equal(1540.00m, invoice.Amount);
        Assert.Null(invoice.PaidAt);
    }

    private static IEnumerable<string?> OutboundTrailersOf(Order order)
    {
        return order.Shipments
            .SelectMany(shipment => shipment.Operations)
            .Where(operation => operation.Type == OperationType.Loading)
            .Select(operation => operation.TrailerNumber)
            .Distinct();
    }

    private Task<Invoice> LoadInvoiceAsync(string orderNumber)
    {
        return seeded.InContextAsync(db => db.Invoices
            .Include(invoice => invoice.Order)
            .SingleAsync(invoice => invoice.Order.Number == orderNumber, TestContext.Current.CancellationToken));
    }
}
