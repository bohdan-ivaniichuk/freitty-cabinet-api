using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// One sample order, built through the public methods of Order in the order things happened. It keeps the seed's
// conventions in one place: draft details carry the instant of the draft, every arrival gets its dock two
// minutes later, an Unloading names the trailer of its arrival, and only Unloading and Loading are billable.
internal sealed class OrderScript
{
    // Submit replaces it before anything is saved, so it never reaches the database.
    private const string PlaceholderDraftNumber = "DRAFT";

    private static readonly TimeSpan DockAssignmentDelay = TimeSpan.FromMinutes(2);

    private readonly DateTimeOffset draftedAt;
    private readonly string floorLead;

    private OrderScript(Order order, DateTimeOffset draftedAt, string floorLead)
    {
        Order = order;
        this.draftedAt = draftedAt;
        this.floorLead = floorLead;
    }

    public Order Order { get; }

    public DueInvoice? DueInvoice { get; private set; }

    public static OrderScript Draft(
        OrderType type,
        CrossDockKind? crossDockKind,
        User createdBy,
        Hub hub,
        string floorLead,
        DateTimeOffset draftedAt,
        string? draftNumber = null)
    {
        Order order = Order.StartDraft(
            draftNumber ?? PlaceholderDraftNumber,
            type,
            crossDockKind,
            createdBy,
            hub.Id,
            draftedAt);
        return new OrderScript(order, draftedAt, floorLead);
    }

    public Shipment AddShipment(string refNumber, params (UnitType UnitType, int Quantity)[] cargo)
    {
        Shipment shipment = Order.AddShipment(refNumber, draftedAt).OrThrow();
        foreach ((UnitType unitType, int quantity) in cargo)
        {
            Order.DeclareCargo(shipment.Id, unitType, quantity, draftedAt).OrThrow();
        }

        return shipment;
    }

    public void Schedule(DateTimeOffset scheduledAt)
    {
        Order.Schedule(scheduledAt, draftedAt).OrThrow();
    }

    public void SetDestination(string city, string region)
    {
        Order.SetDestination(city, region, draftedAt).OrThrow();
    }

    public void ArrangeCarrier(Carrier carrier)
    {
        Order.ArrangeCarrier(carrier.Id, draftedAt).OrThrow();
    }

    public void ArrangeOwnDriver(string driverName)
    {
        Order.ArrangeOwnDriver(driverName, draftedAt).OrThrow();
    }

    public void ArrangeSelfPickup()
    {
        Order.ArrangeSelfPickup(draftedAt).OrThrow();
    }

    public void AddServices(params Service[] services)
    {
        foreach (Service service in services)
        {
            Order.AddService(service.Id, draftedAt).OrThrow();
        }
    }

    public void Submit(string orderNumber, DateTimeOffset submittedAt)
    {
        Order.Submit(orderNumber, submittedAt).OrThrow();
    }

    public void TransferTo(Hub hub, DateTimeOffset transferredAt)
    {
        Order.TransferTo(hub.Id, transferredAt).OrThrow();
    }

    public void Arrive(
        Shipment shipment,
        TrailerType trailerType,
        string truckNumber,
        string trailerNumber,
        DateTimeOffset arrivedAt,
        string dock,
        string bay)
    {
        Order.RecordArrival(shipment.Id, trailerType, truckNumber, trailerNumber, arrivedAt).OrThrow();
        Order.AssignDock(shipment.Id, dock, bay, floorLead, arrivedAt + DockAssignmentDelay).OrThrow();
    }

    public void Unload(Shipment shipment, UnitType unitType, int quantity, int photos, DateTimeOffset unloadedAt)
    {
        Record(shipment, OperationType.Unloading, unitType, quantity, unloadedAt, shipment.TrailerNumber, photos);
    }

    public void DisposeOf(
        Shipment shipment,
        UnitType unitType,
        int quantity,
        int photos,
        int comments,
        DateTimeOffset disposedAt)
    {
        Record(shipment, OperationType.Disposal, unitType, quantity, disposedAt, photos: photos, comments: comments);
    }

    public void Restack(Shipment shipment, UnitType unitType, int quantity, int photos, DateTimeOffset restackedAt)
    {
        Record(shipment, OperationType.Restack, unitType, quantity, restackedAt, photos: photos);
    }

    public void Load(Shipment shipment, UnitType unitType, int quantity, string trailerNumber, DateTimeOffset loadedAt)
    {
        Record(shipment, OperationType.Loading, unitType, quantity, loadedAt, trailerNumber);
    }

    public void SetWarehouseNote(Shipment shipment, string note, DateTimeOffset notedAt)
    {
        Order.SetWarehouseNote(shipment.Id, note, notedAt).OrThrow();
    }

    public void FinishReceiving(Shipment shipment, DateTimeOffset receivedAt)
    {
        Order.FinishReceiving(shipment.Id, receivedAt).OrThrow();
    }

    public void ResolveQuantityDelta(Shipment shipment, DateTimeOffset resolvedAt)
    {
        Alert alert = shipment.Alerts.Single(
            candidate => candidate.Type == AlertType.QuantityDelta && candidate.ResolvedAt is null);
        Order.ResolveAlert(alert.Id, resolvedAt).OrThrow();
    }

    public void AddSupply(SupplyItem supplyItem, int quantity, DateTimeOffset suppliedAt)
    {
        Order.AddSupply(supplyItem, quantity, suppliedAt).OrThrow();
    }

    public void Complete(DateTimeOffset completedAt)
    {
        Order.Complete(completedAt).OrThrow();
    }

    // The invoice itself is issued later, by Invoices: its number depends on every other invoice of the seed.
    public void Invoice(DateTimeOffset issuedAt, DateTimeOffset? paidAt)
    {
        DueInvoice = new DueInvoice(Order, issuedAt, paidAt);
    }

    // The seed's billing rule: moving freight through a trailer is charged per unit, work on the dock is not.
    private static bool IsBillable(OperationType type)
    {
        return type is OperationType.Unloading or OperationType.Loading;
    }

    // Photos and comments are zero unless the script names them.
    private void Record(
        Shipment shipment,
        OperationType type,
        UnitType unitType,
        int quantity,
        DateTimeOffset appliedAt,
        string? trailerNumber = null,
        int photos = 0,
        int comments = 0)
    {
        var operation = new NewOperation(type, unitType, quantity, trailerNumber, IsBillable(type), photos, comments);
        Order.RecordOperation(shipment.Id, operation, appliedAt).OrThrow();
    }
}
