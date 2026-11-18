using Freitty.Domain.Billing;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Support;

// Builds a test's orders through the aggregate under its own company. Every change happens at BuiltAt, before the
// API's clock, unless the test names a later instant; a scheduled date is a value and may be anything.
internal sealed class OrderBuilder(TestData data)
{
    public const int DeclaredStandard = 10;
    public const string TrailerNumber = "TL-1";
    public const string TruckNumber = "TR-1";
    public const string Bay = "B";
    public const string FloorLead = "User 6";
    public const string DestinationCity = "Montreal";
    public const string DestinationRegion = "QC";
    public const string PhotoContentType = "image/png";
    public const long PhotoSizeBytes = 1024;

    public static readonly DateTimeOffset BuiltAt = new(2026, 3, 2, 13, 0, 0, TimeSpan.Zero);

    public Order Draft(OrderType type = OrderType.CrossDock, Hub? hub = null, string? draftNumber = null)
    {
        CrossDockKind? crossDockKind = type == OrderType.CrossDock ? CrossDockKind.Storage : null;
        return Order.StartDraft(
            draftNumber ?? TestData.Unique("DRAFT-"),
            type,
            crossDockKind,
            data.User,
            (hub ?? data.Hub).Id,
            BuiltAt);
    }

    // A draft with everything Submit asks for. A Cross-Dock gets one shipment, a Consolidation two; each declares
    // DeclaredStandard Standard pallets.
    public Order SubmittableDraft(
        OrderType type = OrderType.CrossDock,
        DateTimeOffset? scheduledAt = null,
        Hub? hub = null)
    {
        Order order = Draft(type, hub);
        int shipmentCount = type == OrderType.CrossDock ? 1 : 2;
        for (int position = 1; position <= shipmentCount; position++)
        {
            AddShipment(order, DeclaredStandard);
        }

        order.ArrangeCarrier(data.Carrier.Id, BuiltAt).MustSucceed();
        order.Schedule(scheduledAt ?? CabinetApi.Now.AddDays(1), BuiltAt).MustSucceed();
        order.SetDestination(DestinationCity, DestinationRegion, BuiltAt).MustSucceed();
        return order;
    }

    public Order Ready(
        OrderType type = OrderType.CrossDock,
        DateTimeOffset? scheduledAt = null,
        Hub? hub = null,
        DateTimeOffset? submittedAt = null)
    {
        Order order = SubmittableDraft(type, scheduledAt, hub);
        order.Submit(TestData.Unique("FR"), submittedAt ?? BuiltAt).MustSucceed();
        return order;
    }

    // Schedules a draft that has its shipments and transport, gives it a destination and submits it.
    public static void Submit(Order order, DateTimeOffset scheduledAt, DateTimeOffset? submittedAt = null)
    {
        order.Schedule(scheduledAt, BuiltAt).MustSucceed();
        order.SetDestination(DestinationCity, DestinationRegion, BuiltAt).MustSucceed();
        order.Submit(TestData.Unique("FR"), submittedAt ?? BuiltAt).MustSucceed();
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

    public Order Completed(
        OrderType type = OrderType.CrossDock,
        DateTimeOffset? scheduledAt = null,
        DateTimeOffset? completedAt = null)
    {
        Order order = Received(type, scheduledAt);
        foreach (Shipment shipment in order.Shipments)
        {
            Load(order, shipment, DeclaredStandard);
        }

        order.Complete(completedAt ?? BuiltAt).MustSucceed();
        return order;
    }

    public Shipment AddShipment(Order order, int standard, int extraLarge = 0, string? refNumber = null)
    {
        Shipment shipment = order.AddShipment(refNumber ?? TestData.Unique("REF-"), BuiltAt).MustSucceed();
        order.DeclareCargo(shipment.Id, UnitType.Standard, standard, BuiltAt).MustSucceed();
        if (extraLarge > 0)
        {
            order.DeclareCargo(shipment.Id, UnitType.ExtraLarge, extraLarge, BuiltAt).MustSucceed();
        }

        return shipment;
    }

    public static void Arrive(Order order, Shipment shipment, DateTimeOffset? at = null)
    {
        order.RecordArrival(shipment.Id, TrailerType.DryVan53, TruckNumber, TrailerNumber, at ?? BuiltAt)
            .MustSucceed();
    }

    public static void AssignDock(Order order, Shipment shipment, string dock, DateTimeOffset? at = null)
    {
        order.AssignDock(shipment.Id, dock, Bay, FloorLead, at ?? BuiltAt).MustSucceed();
    }

    public static void Unload(
        Order order,
        Shipment shipment,
        int quantity,
        UnitType unitType = UnitType.Standard,
        int photos = 1,
        DateTimeOffset? at = null)
    {
        Record(order, shipment, OperationType.Unloading, quantity, unitType, TrailerNumber, photos: photos, at: at);
    }

    public static void DisposeOf(
        Order order,
        Shipment shipment,
        int quantity,
        int photos = 1,
        int comments = 0,
        DateTimeOffset? at = null)
    {
        Record(
            order,
            shipment,
            OperationType.Disposal,
            quantity,
            isBillable: false,
            photos: photos,
            comments: comments,
            at: at);
    }

    public static void Restack(Order order, Shipment shipment, int quantity, int photos = 0, DateTimeOffset? at = null)
    {
        Record(order, shipment, OperationType.Restack, quantity, isBillable: false, photos: photos, at: at);
    }

    public static void Load(
        Order order,
        Shipment shipment,
        int quantity,
        UnitType unitType = UnitType.Standard,
        string trailerNumber = TrailerNumber,
        DateTimeOffset? at = null)
    {
        Record(order, shipment, OperationType.Loading, quantity, unitType, trailerNumber, at: at);
    }

    public static void Receive(Order order, Shipment shipment, DateTimeOffset? at = null)
    {
        order.FinishReceiving(shipment.Id, at ?? BuiltAt).MustSucceed();
    }

    public static void ResolveAlerts(Order order)
    {
        foreach (Alert alert in order.Shipments.SelectMany(shipment => shipment.Alerts).ToList())
        {
            order.ResolveAlert(alert.Id, BuiltAt).MustSucceed();
        }
    }

    public static Invoice IssueInvoice(Order order, decimal amount, DateTimeOffset issuedAt, bool paid)
    {
        Invoice invoice = Invoice.Issue(TestData.Unique("INV"), order, amount, issuedAt).MustSucceed();
        if (paid)
        {
            invoice.MarkPaid(issuedAt).MustSucceed();
        }

        return invoice;
    }

    private static void Record(
        Order order,
        Shipment shipment,
        OperationType type,
        int quantity,
        UnitType unitType = UnitType.Standard,
        string? trailerNumber = null,
        bool isBillable = true,
        int photos = 0,
        int comments = 0,
        DateTimeOffset? at = null)
    {
        var operation = new NewOperation(
            type,
            unitType,
            quantity,
            trailerNumber,
            isBillable,
            Photos(photos),
            CommentCount: comments);
        order.RecordOperation(shipment.Id, operation, at ?? BuiltAt).MustSucceed();
    }

    // Rows only: no bytes are stored under these keys. A test that reads a photo back uploads it through the API.
    public static IReadOnlyList<NewPhoto> Photos(int photoCount)
    {
        return Enumerable.Range(0, photoCount)
            .Select(_ => new NewPhoto(TestData.Unique("test/photo-"), PhotoContentType, PhotoSizeBytes))
            .ToList();
    }
}
