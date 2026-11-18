using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Domain.Billing;
using Freitty.Domain.Common;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// Builds a test's orders through the aggregate under its own company. Every change happens at TestData.Now, before
// the API's clock; a scheduled date is a value and may be anything.
internal sealed class OrderBuilder(TestData data)
{
    public const int DeclaredStandard = 10;
    public const string TrailerNumber = "TL-1";

    private static readonly DateTimeOffset Now = TestData.Now;

    public Order Draft(OrderType type = OrderType.CrossDock, Hub? hub = null)
    {
        CrossDockKind? crossDockKind = type == OrderType.CrossDock ? CrossDockKind.Storage : null;
        return Order.StartDraft(
            TestData.Unique("DRAFT-"),
            type,
            crossDockKind,
            data.User,
            (hub ?? data.Hub).Id,
            Now);
    }

    // A Cross-Dock gets one shipment, a Consolidation two; each declares DeclaredStandard Standard pallets.
    public Order Ready(OrderType type = OrderType.CrossDock, DateTimeOffset? scheduledAt = null, Hub? hub = null)
    {
        Order order = Draft(type, hub);
        int shipmentCount = type == OrderType.CrossDock ? 1 : 2;
        for (int position = 1; position <= shipmentCount; position++)
        {
            AddShipment(order, DeclaredStandard);
        }

        Succeed(order.ArrangeCarrier(data.Carrier.Id, Now));
        Submit(order, scheduledAt ?? CabinetApi.Now.AddDays(1));
        return order;
    }

    // Schedules a draft that has its shipments and transport, gives it a destination and submits it.
    public static void Submit(Order order, DateTimeOffset scheduledAt)
    {
        Succeed(order.Schedule(scheduledAt, Now));
        Succeed(order.SetDestination("Montreal", "QC", Now));
        Succeed(order.Submit(TestData.Unique("FR"), Now));
    }

    // In progress, every shipment unloaded as declared with a photo, received, and still on stock.
    public Order Received(OrderType type = OrderType.CrossDock, DateTimeOffset? scheduledAt = null)
    {
        Order order = Ready(type, scheduledAt);
        foreach (Shipment shipment in order.Shipments)
        {
            Unload(order, shipment, DeclaredStandard);
            Receive(order, shipment);
        }

        return order;
    }

    public Order Completed(OrderType type = OrderType.CrossDock, DateTimeOffset? scheduledAt = null)
    {
        Order order = Received(type, scheduledAt);
        foreach (Shipment shipment in order.Shipments)
        {
            Load(order, shipment, DeclaredStandard);
        }

        Succeed(order.Complete(Now));
        return order;
    }

    public Shipment AddShipment(Order order, int standard, int extraLarge = 0, string? refNumber = null)
    {
        Shipment shipment = Value(order.AddShipment(refNumber ?? TestData.Unique("REF-"), Now));
        Succeed(order.DeclareCargo(shipment.Id, UnitType.Standard, standard, Now));
        if (extraLarge > 0)
        {
            Succeed(order.DeclareCargo(shipment.Id, UnitType.ExtraLarge, extraLarge, Now));
        }

        return shipment;
    }

    public static void Arrive(Order order, Shipment shipment)
    {
        Succeed(order.RecordArrival(shipment.Id, TrailerType.DryVan53, "TR-1", TrailerNumber, Now));
    }

    public static void Unload(
        Order order,
        Shipment shipment,
        int quantity,
        UnitType unitType = UnitType.Standard,
        int photos = 1)
    {
        Record(order, shipment, OperationType.Unloading, unitType, quantity, TrailerNumber, photos);
    }

    public static void DisposeOf(Order order, Shipment shipment, int quantity)
    {
        var operation = new NewOperation(
            OperationType.Disposal,
            UnitType.Standard,
            quantity,
            TrailerNumber: null,
            IsBillable: false,
            PhotoCount: 1,
            CommentCount: 0);
        Value(order.RecordOperation(shipment.Id, operation, Now));
    }

    public static void Load(
        Order order,
        Shipment shipment,
        int quantity,
        UnitType unitType = UnitType.Standard,
        string trailerNumber = TrailerNumber)
    {
        Record(order, shipment, OperationType.Loading, unitType, quantity, trailerNumber, photos: 0);
    }

    public static void Receive(Order order, Shipment shipment)
    {
        Succeed(order.FinishReceiving(shipment.Id, Now));
    }

    public static void ResolveAlerts(Order order)
    {
        foreach (Alert alert in order.Shipments.SelectMany(shipment => shipment.Alerts).ToList())
        {
            Succeed(order.ResolveAlert(alert.Id, Now));
        }
    }

    public static Invoice IssueInvoice(Order order, decimal amount, DateTimeOffset issuedAt, bool paid)
    {
        Invoice invoice = Value(Invoice.Issue(TestData.Unique("INV"), order, amount, issuedAt));
        if (paid)
        {
            Succeed(invoice.MarkPaid(issuedAt));
        }

        return invoice;
    }

    private static void Record(
        Order order,
        Shipment shipment,
        OperationType type,
        UnitType unitType,
        int quantity,
        string trailerNumber,
        int photos)
    {
        var operation = new NewOperation(
            type,
            unitType,
            quantity,
            trailerNumber,
            IsBillable: true,
            PhotoCount: photos,
            CommentCount: 0);
        Value(order.RecordOperation(shipment.Id, operation, Now));
    }

    private static void Succeed(Result result)
    {
        TestData.Succeed(result);
    }

    private static T Value<T>(Result<T> result)
        where T : notnull
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }
}
