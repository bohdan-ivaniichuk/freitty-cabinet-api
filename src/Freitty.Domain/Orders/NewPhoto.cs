namespace Freitty.Domain.Orders;

// Plain data: the aggregate checks it as part of its calling contract.
public sealed record NewPhoto(string StorageKey, string ContentType, long SizeBytes);
