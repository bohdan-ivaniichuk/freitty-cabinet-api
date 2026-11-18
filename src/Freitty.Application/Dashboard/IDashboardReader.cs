namespace Freitty.Application.Dashboard;

// The aggregates of the dashboard are hand-written SQL (ADR 0011), so they are read next to the provider. Each
// method is one statement; every figure is scoped to the company, and the instants are cut in the reporting zone.
// The window, the period with its number of buckets and the limit are decided by the caller.
public interface IDashboardReader
{
    Task<DashboardFigures> ReadFiguresAsync(
        Guid companyId,
        DateTimeOffset now,
        TimeSpan rollingWindow,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityBucketDto>> ReadActivityAsync(
        Guid companyId,
        DateTimeOffset now,
        ActivityPeriod period,
        int bucketCount,
        CancellationToken cancellationToken);

    Task<AttentionCounts> ReadAttentionCountsAsync(Guid companyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlertOrderDto>> ReadAlertOrdersAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken);
}
