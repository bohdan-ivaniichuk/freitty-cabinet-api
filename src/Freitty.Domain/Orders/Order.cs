using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Common;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using TransportKinds = Freitty.Domain.Orders.TransportKind;

namespace Freitty.Domain.Orders;

// The aggregate root: shipments, their cargo, ledger and alerts, supply lines and services change only through
// these methods. Every method takes the instant as its last argument and never reads a clock.
public sealed class Order
{
    // The hub works on these; the dashboard counts them as Active.
    public static readonly IReadOnlyList<OrderStatus> ActiveStatuses = [OrderStatus.Ready, OrderStatus.InProgress];

    private const int MinConsolidationShipments = 2;

    private readonly List<Shipment> shipments = [];
    private readonly List<SupplyLine> supplyLines = [];
    private readonly List<OrderService> services = [];

    private Order(
        string draftNumber,
        OrderType type,
        CrossDockKind? crossDockKind,
        User createdBy,
        Guid hubId,
        DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        Number = draftNumber;
        Type = type;
        CrossDockKind = crossDockKind;
        Status = OrderStatus.Draft;
        CompanyId = createdBy.CompanyId;
        CreatedById = createdBy.Id;
        HubId = hubId;
        CreatedAt = now;
        UpdatedAt = now;
        Version = 1;
    }

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = null!;

    public OrderType Type { get; private set; }

    public CrossDockKind? CrossDockKind { get; private set; }

    public OrderStatus Status { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid CreatedById { get; private set; }

    public Guid HubId { get; private set; }

    public Guid? TransferHubId { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public string? DestinationCity { get; private set; }

    public string? DestinationRegion { get; private set; }

    public TransportKind? TransportKind { get; private set; }

    public Guid? CarrierId { get; private set; }

    public string? DriverName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public Company Company { get; private set; } = null!;

    public User CreatedBy { get; private set; } = null!;

    public Hub Hub { get; private set; } = null!;

    public Hub? TransferHub { get; private set; }

    public Carrier? Carrier { get; private set; }

    public IReadOnlyCollection<Shipment> Shipments => shipments.AsReadOnly();

    public IReadOnlyCollection<SupplyLine> SupplyLines => supplyLines.AsReadOnly();

    public IReadOnlyCollection<OrderService> Services => services.AsReadOnly();

    public bool HasOpenAlerts => shipments.Any(shipment => shipment.HasOpenAlert);

    public decimal SupplySubtotal => supplyLines.Sum(line => line.LineTotal);

    public static Order StartDraft(
        string draftNumber,
        OrderType type,
        CrossDockKind? crossDockKind,
        User createdBy,
        Guid hubId,
        DateTimeOffset now)
    {
        string number = Guard.Text(draftNumber);
        Guard.Defined(type);
        EnsureKindMatchesType(type, crossDockKind);
        ArgumentNullException.ThrowIfNull(createdBy);

        return new Order(number, type, crossDockKind, createdBy, hubId, StoredInstant.From(now));
    }

    public Result<Shipment> AddShipment(string refNumber, DateTimeOffset now)
    {
        string trimmedRefNumber = Guard.Text(refNumber);
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        if (Type == OrderType.CrossDock && shipments.Count > 0)
        {
            return OrderErrors.CrossDockHasOne(Number);
        }

        if (shipments.Exists(candidate => candidate.RefNumber == trimmedRefNumber))
        {
            return OrderErrors.DuplicateRefNumber(Number, trimmedRefNumber);
        }

        var shipment = new Shipment(Id, shipments.Count + 1, trimmedRefNumber);
        shipments.Add(shipment);
        Touch(instant);
        return shipment;
    }

    public Result DeclareCargo(Guid shipmentId, UnitType unitType, int quantity, DateTimeOffset now)
    {
        Guard.Defined(unitType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindDraftShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        Shipment shipment = lookup.Value;
        if (shipment.Declares(unitType))
        {
            return OrderErrors.CargoLineAlreadyDeclared(LabelOf(shipment), unitType);
        }

        shipment.DeclareCargo(unitType, quantity);
        Touch(instant);
        return Result.Success();
    }

    public Result Schedule(DateTimeOffset scheduledAt, DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        ScheduledAt = StoredInstant.From(scheduledAt);
        Touch(instant);
        return Result.Success();
    }

    public Result SetDestination(string city, string region, DateTimeOffset now)
    {
        string trimmedCity = Guard.Text(city);
        string trimmedRegion = Guard.Text(region);
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        DestinationCity = trimmedCity;
        DestinationRegion = trimmedRegion;
        Touch(instant);
        return Result.Success();
    }

    public Result ArrangeCarrier(Guid carrierId, DateTimeOffset now)
    {
        return ArrangeTransport(TransportKinds.Carrier, carrierId, driverName: null, now);
    }

    public Result ArrangeOwnDriver(string driverName, DateTimeOffset now)
    {
        string trimmedDriverName = Guard.Text(driverName);
        return ArrangeTransport(TransportKinds.OwnDriver, carrierId: null, trimmedDriverName, now);
    }

    public Result ArrangeSelfPickup(DateTimeOffset now)
    {
        return ArrangeTransport(TransportKinds.SelfPickup, carrierId: null, driverName: null, now);
    }

    public Result AddService(Guid serviceId, DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        if (services.Exists(service => service.ServiceId == serviceId))
        {
            return OrderErrors.ServiceAlreadyAdded(Number, serviceId);
        }

        services.Add(new OrderService(Id, serviceId));
        Touch(instant);
        return Result.Success();
    }

    // Asked before an official number is taken, so a refused submit uses none; Submit asks it again.
    public Result CanSubmit()
    {
        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        List<string> missingParts = MissingForSubmit();
        if (missingParts.Count > 0)
        {
            return OrderErrors.NotReadyToSubmit(Number, missingParts);
        }

        return Result.Success();
    }

    public Result Submit(string orderNumber, DateTimeOffset now)
    {
        string officialNumber = Guard.Text(orderNumber);
        DateTimeOffset instant = AcceptInstant(now);

        Result check = CanSubmit();
        if (check.IsFailure)
        {
            return check;
        }

        Number = officialNumber;
        Status = OrderStatus.Ready;
        SubmittedAt = instant;
        Touch(instant);
        return Result.Success();
    }

    public Result RecordArrival(
        Guid shipmentId,
        TrailerType trailerType,
        string truckNumber,
        string trailerNumber,
        DateTimeOffset now)
    {
        Guard.Defined(trailerType);
        string normalizedTruckNumber = Guard.VehicleNumber(truckNumber);
        string normalizedTrailerNumber = Guard.VehicleNumber(trailerNumber);
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindActiveShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        // Later trucks of the same shipment show in the trailer numbers of their Unloadings.
        Shipment shipment = lookup.Value;
        if (shipment.HasArrived)
        {
            return OrderErrors.AlreadyArrived(LabelOf(shipment));
        }

        shipment.RecordArrival(trailerType, normalizedTruckNumber, normalizedTrailerNumber, instant);
        RecordHubWork(instant);
        return Result.Success();
    }

    public Result AssignDock(Guid shipmentId, string dock, string bay, string floorLead, DateTimeOffset now)
    {
        string trimmedDock = Guard.Text(dock);
        string trimmedBay = Guard.Text(bay);
        string trimmedFloorLead = Guard.Text(floorLead);
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindActiveShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        lookup.Value.AssignDock(trimmedDock, trimmedBay, trimmedFloorLead, instant);
        RecordHubWork(instant);
        return Result.Success();
    }

    public Result<Operation> RecordOperation(Guid shipmentId, NewOperation newOperation, DateTimeOffset now)
    {
        NewOperation checkedOperation = CheckContract(newOperation);
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindActiveShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        // Unloading has no upper bound: receiving more than declared is exactly what a Quantity delta records.
        Shipment shipment = lookup.Value;
        int onStock = shipment.OnStock(checkedOperation.UnitType);
        if (checkedOperation.Type != OperationType.Unloading && checkedOperation.Quantity > onStock)
        {
            return OrderErrors.ExceedsStock(
                LabelOf(shipment),
                checkedOperation.Type,
                checkedOperation.UnitType,
                checkedOperation.Quantity,
                onStock);
        }

        Operation operation = shipment.RecordOperation(checkedOperation, instant);
        RecordHubWork(instant);
        return operation;
    }

    // Photos come after the operation they document. The caller names where their bytes are kept: storing them is
    // not a rule of the order.
    public Result<PhotoAttachment> AttachPhotos(Guid operationId, IReadOnlyList<NewPhoto> photos, DateTimeOffset now)
    {
        CheckContract(photos);
        ArgumentOutOfRangeException.ThrowIfZero(photos.Count, nameof(photos));
        DateTimeOffset instant = AcceptInstant(now);

        if (!IsActive)
        {
            return OrderErrors.NotActive(Number, Status);
        }

        foreach (Shipment shipment in shipments)
        {
            if (shipment.FindOperation(operationId) is { } operation)
            {
                return AttachPhotos(shipment, operation, photos, instant);
            }
        }

        return OrderErrors.OperationNotFound(Number, operationId);
    }

    // A shipment that never arrived is finished with no Unloading, which reports it as short.
    public Result FinishReceiving(Guid shipmentId, DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindActiveShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        Shipment shipment = lookup.Value;
        if (shipment.IsReceived)
        {
            return OrderErrors.AlreadyReceived(LabelOf(shipment));
        }

        shipment.FinishReceiving(instant);
        RecordHubWork(instant);
        return Result.Success();
    }

    public Result SetWarehouseNote(Guid shipmentId, string note, DateTimeOffset now)
    {
        string trimmedNote = Guard.Text(note);
        DateTimeOffset instant = AcceptInstant(now);

        Result<Shipment> lookup = FindActiveShipment(shipmentId);
        if (lookup.IsFailure)
        {
            return lookup.Error;
        }

        lookup.Value.SetWarehouseNote(trimmedNote);
        RecordHubWork(instant);
        return Result.Success();
    }

    public Result ResolveAlert(Guid alertId, DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (!IsActive)
        {
            return OrderErrors.NotActive(Number, Status);
        }

        Alert? alert = shipments
            .SelectMany(shipment => shipment.Alerts)
            .FirstOrDefault(candidate => candidate.Id == alertId);
        if (alert is null)
        {
            return OrderErrors.AlertNotFound(Number, alertId);
        }

        if (!alert.IsOpen)
        {
            Shipment shipment = shipments.First(candidate => candidate.Id == alert.ShipmentId);
            return OrderErrors.AlertAlreadyResolved(LabelOf(shipment), alert.Type);
        }

        alert.Resolve(instant);
        Touch(instant);
        return Result.Success();
    }

    public Result Complete(DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.InProgress)
        {
            return OrderErrors.NotInProgress(Number, Status);
        }

        Shipment? notReceived = shipments.Find(shipment => !shipment.IsReceived);
        if (notReceived is not null)
        {
            return OrderErrors.ReceivingNotFinished(Number, LabelOf(notReceived));
        }

        Shipment? withStock = shipments.Find(shipment => shipment.HasStock);
        if (withStock is not null)
        {
            return OrderErrors.StockRemaining(Number, LabelOf(withStock));
        }

        Shipment? withOpenAlert = shipments.Find(shipment => shipment.HasOpenAlert);
        if (withOpenAlert is not null)
        {
            return OrderErrors.OpenAlerts(Number, LabelOf(withOpenAlert));
        }

        Status = OrderStatus.Completed;
        CompletedAt = instant;
        Touch(instant);
        return Result.Success();
    }

    // Not draft-only: on a submitted order the transfer is arranged by platform staff.
    public Result TransferTo(Guid transferHubId, DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (Status == OrderStatus.Completed)
        {
            return OrderErrors.AlreadyCompleted(Number);
        }

        if (transferHubId == HubId)
        {
            return OrderErrors.SameHub(Number);
        }

        TransferHubId = transferHubId;
        Touch(instant);
        return Result.Success();
    }

    // Each call is its own line with the price the item has now, so the same item may be added again.
    public Result AddSupply(SupplyItem supplyItem, int quantity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(supplyItem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        DateTimeOffset instant = AcceptInstant(now);

        if (Status == OrderStatus.Completed)
        {
            return OrderErrors.AlreadyCompleted(Number);
        }

        supplyLines.Add(new SupplyLine(Id, supplyItem, quantity));
        Touch(instant);
        return Result.Success();
    }

    private bool IsActive => ActiveStatuses.Contains(Status);

    private static void EnsureKindMatchesType(OrderType type, CrossDockKind? crossDockKind)
    {
        if (type == OrderType.CrossDock && crossDockKind is null)
        {
            throw new ArgumentException("A Cross-Dock order needs a kind: Storage or Pickup.", nameof(crossDockKind));
        }

        if (type == OrderType.Consolidation && crossDockKind is not null)
        {
            throw new ArgumentException("A Consolidation order has no Cross-Dock kind.", nameof(crossDockKind));
        }

        if (crossDockKind is { } kind)
        {
            Guard.Defined(kind, nameof(crossDockKind));
        }
    }

    private static NewOperation CheckContract(NewOperation newOperation)
    {
        ArgumentNullException.ThrowIfNull(newOperation);
        Guard.Defined(newOperation.Type);
        Guard.Defined(newOperation.UnitType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newOperation.Quantity);
        CheckContract(newOperation.Photos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newOperation.Photos.Count, Operation.MaxPhotoCount);
        ArgumentOutOfRangeException.ThrowIfNegative(newOperation.CommentCount);

        bool needsTrailer = Operation.RequiresTrailer(newOperation.Type);
        if (!needsTrailer && newOperation.TrailerNumber is not null)
        {
            throw new ArgumentException(
                $"A {newOperation.Type} does not move freight through a trailer and takes no trailer number.",
                nameof(newOperation));
        }

        if (!needsTrailer)
        {
            return newOperation;
        }

        if (string.IsNullOrWhiteSpace(newOperation.TrailerNumber))
        {
            throw new ArgumentException(
                $"A {newOperation.Type} moves freight between a trailer and the dock and needs a trailer number.",
                nameof(newOperation));
        }

        return newOperation with { TrailerNumber = Guard.VehicleNumber(newOperation.TrailerNumber) };
    }

    private static void CheckContract(IReadOnlyList<NewPhoto> photos)
    {
        ArgumentNullException.ThrowIfNull(photos);
        foreach (NewPhoto photo in photos)
        {
            ArgumentNullException.ThrowIfNull(photo, nameof(photos));
            ArgumentException.ThrowIfNullOrWhiteSpace(photo.StorageKey, nameof(photos));
            ArgumentException.ThrowIfNullOrWhiteSpace(photo.ContentType, nameof(photos));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(photo.SizeBytes, nameof(photos));
        }
    }

    // Time moves forward within an order: this one rule keeps the milestones, the Operations and the Alerts in
    // order.
    private DateTimeOffset AcceptInstant(DateTimeOffset now)
    {
        DateTimeOffset instant = StoredInstant.From(now);
        if (instant < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now),
                now,
                $"Order {Number} was last changed at {UpdatedAt:O}; a change cannot happen before that.");
        }

        return instant;
    }

    private Result ArrangeTransport(
        TransportKind transportKind,
        Guid? carrierId,
        string? driverName,
        DateTimeOffset now)
    {
        DateTimeOffset instant = AcceptInstant(now);

        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        TransportKind = transportKind;
        CarrierId = carrierId;
        DriverName = driverName;
        Touch(instant);
        return Result.Success();
    }

    private Result<PhotoAttachment> AttachPhotos(
        Shipment shipment,
        Operation operation,
        IReadOnlyList<NewPhoto> photos,
        DateTimeOffset now)
    {
        if (!operation.HasRoomFor(photos.Count))
        {
            return OrderErrors.PhotoLimit(LabelOf(shipment), operation.Type, operation.PhotoCount, photos.Count);
        }

        PhotoAttachment attachment = shipment.AttachPhotos(operation, photos, now);
        Touch(now);
        return attachment;
    }

    private List<string> MissingForSubmit()
    {
        var missingParts = new List<string>();
        if (ScheduledAt is null)
        {
            missingParts.Add("a scheduled date");
        }

        if (DestinationCity is null)
        {
            missingParts.Add("a destination");
        }

        if (TransportKind is null)
        {
            missingParts.Add("a transport arrangement");
        }

        int requiredShipments = Type == OrderType.Consolidation ? MinConsolidationShipments : 1;
        if (shipments.Count < requiredShipments)
        {
            missingParts.Add(requiredShipments == 1
                ? "a shipment"
                : $"at least {requiredShipments} shipments for a Consolidation");
        }

        missingParts.AddRange(shipments
            .Where(shipment => shipment.CargoLines.Count == 0)
            .Select(shipment => $"cargo on {LabelOf(shipment)}"));
        return missingParts;
    }

    private Result<Shipment> FindDraftShipment(Guid shipmentId)
    {
        if (Status != OrderStatus.Draft)
        {
            return OrderErrors.NotDraft(Number, Status);
        }

        return FindShipment(shipmentId);
    }

    private Result<Shipment> FindActiveShipment(Guid shipmentId)
    {
        if (!IsActive)
        {
            return OrderErrors.NotActive(Number, Status);
        }

        return FindShipment(shipmentId);
    }

    private Result<Shipment> FindShipment(Guid shipmentId)
    {
        Shipment? shipment = shipments.Find(candidate => candidate.Id == shipmentId);
        if (shipment is null)
        {
            return OrderErrors.ShipmentNotFound(Number, shipmentId);
        }

        return shipment;
    }

    // Any action of the hub starts a Ready order.
    private void RecordHubWork(DateTimeOffset now)
    {
        if (Status == OrderStatus.Ready)
        {
            Status = OrderStatus.InProgress;
            StartedAt = now;
        }

        Touch(now);
    }

    // Every change writes the order row, also when two changes carry the same instant.
    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    private string LabelOf(Shipment shipment)
    {
        return Shipment.LabelOf(Number, shipment.Position);
    }
}
