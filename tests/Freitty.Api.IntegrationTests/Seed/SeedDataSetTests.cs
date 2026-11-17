using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Seed;

[Collection(SeededDatabaseCollection.Name)]
public sealed class SeedDataSetTests(SeededDatabase seeded)
{
    private const decimal BillableUnitRate = 14.00m;
    private const int MinSuppliesSold = 8;

    [Fact]
    public async Task SeedingCreatesReferenceData()
    {
        int[] counts = await seeded.InContextAsync(async db => new[]
        {
            await db.Companies.CountAsync(CancellationToken),
            await db.Users.CountAsync(CancellationToken),
            await db.Hubs.CountAsync(CancellationToken),
            await db.Carriers.CountAsync(CancellationToken),
            await db.Services.CountAsync(CancellationToken),
            await db.SupplyItems.CountAsync(CancellationToken),
        });

        Assert.Equal([1, 4, 2, 3, 2, 16], counts);
    }

    [Fact]
    public async Task SeventyOrdersAreSeededInEveryStatus()
    {
        Dictionary<OrderStatus, int> countsByStatus = await seeded.InContextAsync(db => db.Orders
            .GroupBy(order => order.Status)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), CancellationToken));

        Assert.Equal(
            new Dictionary<OrderStatus, int>
            {
                [OrderStatus.Draft] = 1,
                [OrderStatus.Ready] = 2,
                [OrderStatus.InProgress] = 5,
                [OrderStatus.Completed] = 62,
            },
            countsByStatus);
    }

    [Fact]
    public async Task OnlyFR001674AndFR001383HaveOpenAlerts()
    {
        string[] numbers = await seeded.InContextAsync(db => db.Orders
            .Where(order => order.Shipments.Any(shipment => shipment.Alerts.Any(alert => alert.ResolvedAt == null)))
            .Select(order => order.Number)
            .ToArrayAsync(CancellationToken));

        Assert.Equivalent(new[] { "FR001383", "FR001674" }, numbers, strict: true);
    }

    [Fact]
    public async Task CompletedOrdersFillBothThirtyDayPeriods()
    {
        DateTimeOffset monthAgo = SeededDatabase.Now.AddDays(-30);
        DateTimeOffset twoMonthsAgo = SeededDatabase.Now.AddDays(-60);

        int[] counts = await seeded.InContextAsync(async db => new[]
        {
            await db.Orders.CountAsync(
                order => order.CompletedAt >= monthAgo && order.CompletedAt < SeededDatabase.Now,
                CancellationToken),
            await db.Orders.CountAsync(
                order => order.CompletedAt >= twoMonthsAgo && order.CompletedAt < monthAgo,
                CancellationToken),
        });

        Assert.Equal([24, 20], counts);
    }

    [Fact]
    public async Task OnlyInvoiceOfFR001668IsUnpaid()
    {
        var invoices = await seeded.InContextAsync(db => db.Invoices
            .Select(invoice => new { OrderNumber = invoice.Order.Number, invoice.PaidAt })
            .ToListAsync(CancellationToken));

        Assert.Equal(62, invoices.Count);
        Assert.Equal("FR001668", Assert.Single(invoices, invoice => invoice.PaidAt is null).OrderNumber);
    }

    [Fact]
    public async Task EveryCompletedOrderHasOneInvoiceForSuppliesAndBillableUnits()
    {
        Guid[] completedOrderIds = await seeded.InContextAsync(db => db.Orders
            .Where(order => order.Status == OrderStatus.Completed)
            .Select(order => order.Id)
            .ToArrayAsync(CancellationToken));

        foreach (Guid orderId in completedOrderIds)
        {
            (Order order, decimal[] amounts) = await seeded.InContextAsync(async db => (
                await db.LoadOrderAsync(orderId),
                await db.Invoices
                    .Where(invoice => invoice.OrderId == orderId)
                    .Select(invoice => invoice.Amount)
                    .ToArrayAsync(CancellationToken)));

            Assert.Equal(order.SupplySubtotal + (BillableUnitsOf(order) * BillableUnitRate), Assert.Single(amounts));
        }
    }

    [Fact]
    public async Task EachHubHasReadyInProgressAndCompletedOrders()
    {
        var statusesByHub = await seeded.InContextAsync(db => db.Orders
            .Select(order => new { HubName = order.Hub.Name, order.Status })
            .Distinct()
            .ToListAsync(CancellationToken));

        foreach (string hubName in new[] { "Markham", "Toronto" })
        {
            Assert.Superset(
                new HashSet<OrderStatus> { OrderStatus.Ready, OrderStatus.InProgress, OrderStatus.Completed },
                statusesByHub.Where(entry => entry.HubName == hubName).Select(entry => entry.Status).ToHashSet());
        }
    }

    [Fact]
    public async Task EveryUserCreatesOrders()
    {
        bool everyUserCreates = await seeded.InContextAsync(db => db.Users
            .AllAsync(user => db.Orders.Any(order => order.CreatedById == user.Id), CancellationToken));

        Assert.True(everyUserCreates);
    }

    [Fact]
    public async Task EveryTransportKindAndCarrierIsUsed()
    {
        var used = await seeded.InContextAsync(async db => new
        {
            TransportKinds = await db.Orders
                .Where(order => order.TransportKind != null)
                .Select(order => order.TransportKind!.Value)
                .Distinct()
                .ToListAsync(CancellationToken),
            EveryCarrier = await db.Carriers
                .AllAsync(carrier => db.Orders.Any(order => order.CarrierId == carrier.Id), CancellationToken),
        });

        Assert.Equivalent(Enum.GetValues<TransportKind>(), used.TransportKinds, strict: true);
        Assert.True(used.EveryCarrier);
    }

    [Fact]
    public async Task EveryUnitTypeAndTrailerTypeIsUsed()
    {
        var used = await seeded.InContextAsync(async db => new
        {
            UnitTypes = await db.Orders
                .SelectMany(order => order.Shipments.SelectMany(shipment => shipment.CargoLines))
                .Select(line => line.UnitType)
                .Distinct()
                .ToListAsync(CancellationToken),
            TrailerTypes = await db.Orders
                .SelectMany(order => order.Shipments)
                .Where(shipment => shipment.TrailerType != null)
                .Select(shipment => shipment.TrailerType!.Value)
                .Distinct()
                .ToListAsync(CancellationToken),
        });

        Assert.Equivalent(Enum.GetValues<UnitType>(), used.UnitTypes, strict: true);
        Assert.Equivalent(Enum.GetValues<TrailerType>(), used.TrailerTypes, strict: true);
    }

    [Fact]
    public async Task EveryCrossDockKindAndServiceIsUsed()
    {
        var used = await seeded.InContextAsync(async db => new
        {
            CrossDockKinds = await db.Orders
                .Where(order => order.CrossDockKind != null)
                .Select(order => order.CrossDockKind!.Value)
                .Distinct()
                .ToListAsync(CancellationToken),
            EveryService = await db.Services
                .AllAsync(
                    service => db.Orders
                        .SelectMany(order => order.Services)
                        .Any(orderService => orderService.ServiceId == service.Id),
                    CancellationToken),
        });

        Assert.Equivalent(Enum.GetValues<CrossDockKind>(), used.CrossDockKinds, strict: true);
        Assert.True(used.EveryService);
    }

    [Fact]
    public async Task AtLeastEightSupplyItemsAreSold()
    {
        int itemsSold = await seeded.InContextAsync(db => db.Orders
            .SelectMany(order => order.SupplyLines)
            .Select(line => line.SupplyItemId)
            .Distinct()
            .CountAsync(CancellationToken));

        Assert.InRange(itemsSold, MinSuppliesSold, int.MaxValue);
    }

    [Fact]
    public async Task RefNumbersAreUnique()
    {
        string[] refNumbers = await seeded.InContextAsync(db => db.Orders
            .SelectMany(order => order.Shipments)
            .Select(shipment => shipment.RefNumber)
            .ToArrayAsync(CancellationToken));

        Assert.Equal(refNumbers.Length, refNumbers.Distinct().Count());
    }

    [Fact]
    public async Task EveryArrivedShipmentHasDockAssignment()
    {
        var arrived = await seeded.InContextAsync(db => db.Orders
            .SelectMany(order => order.Shipments)
            .Where(shipment => shipment.ArrivedAt != null)
            .Select(shipment => new { shipment.RefNumber, shipment.DockAssignedAt })
            .ToListAsync(CancellationToken));

        Assert.NotEmpty(arrived);
        Assert.All(arrived, shipment => Assert.NotNull(shipment.DockAssignedAt));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static int BillableUnitsOf(Order order)
    {
        return order.Shipments
            .SelectMany(shipment => shipment.Operations)
            .Where(operation => operation.IsBillable)
            .Sum(operation => operation.Quantity);
    }
}
