namespace Freitty.Application.Orders.GetPhoto;

// The bytes of a photo as a stream, which the receiver disposes, with the type and size they are served with.
public sealed record PhotoContentDto(Stream Content, string ContentType, long SizeBytes);
