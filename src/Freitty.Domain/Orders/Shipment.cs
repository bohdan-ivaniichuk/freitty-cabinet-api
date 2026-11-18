namespace Freitty.Domain.Orders;

public sealed class Shipment
{
    public const char LabelSeparator = '-';

    private static readonly UnitType[] AllUnitTypes = Enum.GetValues<UnitType>();

    private readonly List<CargoLine> cargoLines = [];
    private readonly List<Operation> operations = [];
    private readonly List<Alert> alerts = [];

    internal Shipment(Guid orderId, int position, string refNumber)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Position = position;
        RefNumber = refNumber;
    }

    private Shipment()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public int Position { get; private set; }

    public string RefNumber { get; private set; } = null!;

    public string? WarehouseNote { get; private set; }

    public TrailerType? TrailerType { get; private set; }

    public string? TruckNumber { get; private set; }

    public string? TrailerNumber { get; private set; }

    public DateTimeOffset? ArrivedAt { get; private set; }

    public DateTimeOffset? ReceivedAt { get; private set; }

    public string? Dock { get; private set; }

    public string? Bay { get; private set; }

    public string? FloorLead { get; private set; }

    public DateTimeOffset? DockAssignedAt { get; private set; }

    public IReadOnlyCollection<CargoLine> CargoLines => cargoLines.AsReadOnly();

    public IReadOnlyCollection<Operation> Operations => operations.AsReadOnly();

    public IReadOnlyCollection<Alert> Alerts => alerts.AsReadOnly();

    public bool HasQuantityDelta => KnownDeltas().Any(delta => delta != 0);

    internal bool HasArrived => ArrivedAt is not null;

    internal bool IsReceived => ReceivedAt is not null;

    internal bool HasStock => AllUnitTypes.Any(unitType => OnStock(unitType) > 0);

    internal bool HasOpenAlert => alerts.Any(alert => alert.IsOpen);

    // How a shipment is named to a person: <number>-<position> for every shipment, the single one of a Cross-Dock
    // included.
    public static string LabelOf(string orderNumber, int position)
    {
        return $"{orderNumber}{LabelSeparator}{position}";
    }

    public int DeclaredQuantity(UnitType unitType)
    {
        return cargoLines.Where(line => line.UnitType == unitType).Sum(line => line.Quantity);
    }

    public int ActualQuantity(UnitType unitType)
    {
        return operations
            .Where(operation => operation.Type == OperationType.Unloading && operation.UnitType == unitType)
            .Sum(operation => operation.Quantity);
    }

    public int OnStock(UnitType unitType)
    {
        return operations.Where(operation => operation.UnitType == unitType).Sum(operation => operation.StockChange);
    }

    internal bool Declares(UnitType unitType)
    {
        return cargoLines.Exists(line => line.UnitType == unitType);
    }

    internal void DeclareCargo(UnitType unitType, int quantity)
    {
        cargoLines.Add(new CargoLine(Id, unitType, quantity));
    }

    internal void RecordArrival(TrailerType trailerType, string truckNumber, string trailerNumber, DateTimeOffset now)
    {
        TrailerType = trailerType;
        TruckNumber = truckNumber;
        TrailerNumber = trailerNumber;
        ArrivedAt = now;
    }

    internal void AssignDock(string dock, string bay, string floorLead, DateTimeOffset now)
    {
        Dock = dock;
        Bay = bay;
        FloorLead = floorLead;
        DockAssignedAt = now;
    }

    internal void SetWarehouseNote(string note)
    {
        WarehouseNote = note;
    }

    // A shortage becomes known only here: until the hub finishes receiving, the rest may be on the next truck.
    internal void FinishReceiving(DateTimeOffset now)
    {
        int[] deltasBefore = KnownDeltas();
        ReceivedAt = now;
        FollowQuantityDelta(deltasBefore, now);
    }

    internal Operation RecordOperation(NewOperation newOperation, DateTimeOffset now)
    {
        bool changesActual = newOperation.Type == OperationType.Unloading;
        int[] deltasBefore = KnownDeltas();

        var operation = new Operation(Id, newOperation, now);
        operations.Add(operation);

        if (changesActual)
        {
            FollowQuantityDelta(deltasBefore, now);
        }

        if (Operation.TypesRequiringPhoto.Contains(operation.Type) && operation.PhotoCount == 0)
        {
            RaiseUnlessOpen(AlertType.MissingPhoto, now);
        }

        return operation;
    }

    // The alert follows changes of the delta, not its presence: once resolved by hand, the same delta does not
    // raise it again, while a different one does — also a smaller one, since the client was told about a state
    // that no longer holds.
    private void FollowQuantityDelta(int[] deltasBefore, DateTimeOffset now)
    {
        int[] deltasAfter = KnownDeltas();
        Alert? openAlert = FindOpenAlert(AlertType.QuantityDelta);

        if (deltasAfter.All(delta => delta == 0))
        {
            openAlert?.Resolve(now);
            return;
        }

        if (!deltasAfter.SequenceEqual(deltasBefore))
        {
            RaiseUnlessOpen(AlertType.QuantityDelta, now);
        }
    }

    private void RaiseUnlessOpen(AlertType type, DateTimeOffset now)
    {
        if (FindOpenAlert(type) is null)
        {
            alerts.Add(new Alert(Id, type, now));
        }
    }

    private Alert? FindOpenAlert(AlertType type)
    {
        return alerts.Find(alert => alert.Type == type && alert.IsOpen);
    }

    // One value per Unit Type, in enum order. An excess counts as soon as it is unloaded; a shortage only once
    // the shipment is received. A Unit Type that was not declared counts as declared zero.
    private int[] KnownDeltas()
    {
        return AllUnitTypes.Select(KnownDelta).ToArray();
    }

    private int KnownDelta(UnitType unitType)
    {
        int delta = ActualQuantity(unitType) - DeclaredQuantity(unitType);
        return delta > 0 || IsReceived ? delta : 0;
    }
}
