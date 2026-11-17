namespace Freitty.Domain.Common;

// An instant is kept the way PostgreSQL stores it: Npgsql refuses a non-zero offset for timestamptz, and
// timestamptz keeps microseconds, so an instant with finer ticks would not equal itself after a round trip.
internal static class StoredInstant
{
    public static DateTimeOffset From(DateTimeOffset instant)
    {
        DateTimeOffset utc = instant.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
