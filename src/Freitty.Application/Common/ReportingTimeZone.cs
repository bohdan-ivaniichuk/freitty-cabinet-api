namespace Freitty.Application.Common;

// The one zone in which "today", "this week" and chart buckets are cut (ADR 0010). Its id is an IANA id, checked
// at startup, because PostgreSQL is handed the same id.
public sealed record ReportingTimeZone(TimeZoneInfo Zone)
{
    public string Id => Zone.Id;
}
