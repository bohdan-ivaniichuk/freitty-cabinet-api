namespace Freitty.Application.Common;

// Half-open: From is inside, To is not. Both are UTC.
public sealed record InstantRange(DateTimeOffset From, DateTimeOffset To);
