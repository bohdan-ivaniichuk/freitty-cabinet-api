namespace Freitty.Domain.Orders;

public sealed class Operation
{
    public const int MaxTrailerNumberLength = 20;

    // The demo has no sign-in: with the limits of an upload, this bounds what one operation can be made to hold.
    public const int MaxPhotoCount = 10;

    // Unloading and Loading move freight between a trailer and the dock, so they record the trailer number.
    private static readonly IReadOnlySet<OperationType> TypesWithTrailer =
        new HashSet<OperationType> { OperationType.Unloading, OperationType.Loading };

    // These document the condition of freight; recorded without a photo, they raise a MissingPhoto alert.
    internal static readonly IReadOnlySet<OperationType> TypesRequiringPhoto =
        new HashSet<OperationType> { OperationType.Unloading, OperationType.Disposal };

    private readonly List<Photo> photos = [];

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
        CommentCount = newOperation.CommentCount;
        AttachPhotos(newOperation.Photos, appliedAt);
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

    // Stored, and always the number of photos: the list, the detail and the totals read the count alone. It is
    // counted on, not from the Photos collection, so it holds also when the photos already attached were not loaded.
    public int PhotoCount { get; private set; }

    public int CommentCount { get; private set; }

    public IReadOnlyCollection<Photo> Photos => photos.AsReadOnly();

    public static bool RequiresTrailer(OperationType type)
    {
        return TypesWithTrailer.Contains(type);
    }

    internal bool HasRoomFor(int photoCount)
    {
        return PhotoCount + photoCount <= MaxPhotoCount;
    }

    internal List<Photo> AttachPhotos(IEnumerable<NewPhoto> newPhotos, DateTimeOffset now)
    {
        List<Photo> attachedPhotos = newPhotos.Select(newPhoto => new Photo(Id, newPhoto, now)).ToList();
        photos.AddRange(attachedPhotos);
        PhotoCount += attachedPhotos.Count;
        return attachedPhotos;
    }

    // A Restack rearranges pallets and does not change their count.
    internal int StockChange => Type switch
    {
        OperationType.Unloading => Quantity,
        OperationType.Disposal or OperationType.Loading => -Quantity,
        OperationType.Restack => 0,
        _ => throw new InvalidOperationException($"Operation {Id} has an unknown type {Type}."),
    };
}
