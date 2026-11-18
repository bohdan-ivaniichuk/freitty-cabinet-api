namespace Freitty.Application.Orders.AttachPhotos;

// A file as the client sent it. The name is only for messages; what the file is, is read from its bytes. Every call
// of OpenReadStream starts at the first byte.
public sealed record PhotoFile(string FileName, long SizeBytes, Func<Stream> OpenReadStream);
