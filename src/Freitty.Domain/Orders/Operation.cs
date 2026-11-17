namespace Freitty.Domain.Orders;

public sealed class Operation
{
    // Unloading and Loading move freight between a trailer and the dock, so they record the trailer number.
    internal static readonly IReadOnlySet<OperationType> TypesWithTrailer =
        new HashSet<OperationType> { OperationType.Unloading, OperationType.Loading };

    // These document the condition of freight; recorded without a photo, they raise a MissingPhoto alert.
    internal static readonly IReadOnlySet<OperationType> TypesRequiringPhoto =
        new HashSet<OperationType> { OperationType.Unloading, OperationType.Disposal };

    internal Operation(Guid shipmentId, NewOperation newOperation, DateTimeOffset appliedAt)
    {
        Id = Guid.CreateVersion7();
        ShipmentId = shipmentId;
        Type = newOperation.Type;
        UnitType = newOperation.UnitType;
        Quantity = newOperation.Quantity;
        TrailerNumber = newOperation.TrailerNumber;
        AppliedAt = appliedAt;
        IsBillable = newOperation.IsBillable;
        PhotoCount = newOperation.PhotoCount;
        CommentCount = newOperation.CommentCount;
    }

    private Operation()
    {
    }

    public Guid Id { get; private set; }

    public Guid ShipmentId { get; private set; }

    public OperationType Type { get; private set; }

    public UnitType UnitType { get; private set; }

    public int Quantity { get; private set; }

    public string? TrailerNumber { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }

    public bool IsBillable { get; private set; }

    public int PhotoCount { get; private set; }

    public int CommentCount { get; private set; }

    // A Restack rearranges pallets and does not change their count.
    internal int StockChange => Type switch
    {
        OperationType.Unloading => Quantity,
        OperationType.Disposal or OperationType.Loading => -Quantity,
        OperationType.Restack => 0,
        _ => throw new InvalidOperationException($"Operation {Id} has an unknown type {Type}."),
    };
}
