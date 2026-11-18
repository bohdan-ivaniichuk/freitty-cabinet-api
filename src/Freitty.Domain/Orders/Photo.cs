namespace Freitty.Domain.Orders;

// A photo of an operation: the bytes live in blob storage under StorageKey, the row says what they are. The content
// type is the one found in the bytes, not the one a client named.
public sealed class Photo
{
    internal Photo(Guid operationId, NewPhoto newPhoto, DateTimeOffset uploadedAt)
    {
        Id = Guid.CreateVersion7();
        OperationId = operationId;
        StorageKey = newPhoto.StorageKey;
        ContentType = newPhoto.ContentType;
        SizeBytes = newPhoto.SizeBytes;
        UploadedAt = uploadedAt;
    }

    private Photo()
    {
    }

    public Guid Id { get; private set; }

    public Guid OperationId { get; private set; }

    public string StorageKey { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }
}
