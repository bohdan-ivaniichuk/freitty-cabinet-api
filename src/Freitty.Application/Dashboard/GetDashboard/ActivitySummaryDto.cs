namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record ActivitySummaryDto(
    int CompletedLast30Days,
    int? CompletedChangePercent,
    decimal SpendLast30Days,
    decimal? AverageSpendPerOrder,
    ActivityBucketDto? BestBucket);
