namespace Freitty.Application.Orders.AttachPhotos;

// ContentType is the type found in the bytes, the one the photo is served with.
public sealed record AttachedPhotoDto(Guid Id, string ContentType, long SizeBytes);
