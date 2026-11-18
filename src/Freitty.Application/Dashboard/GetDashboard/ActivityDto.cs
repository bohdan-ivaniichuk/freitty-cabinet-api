namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record ActivityDto(
    ActivityPeriod Period,
    IReadOnlyList<ActivityBucketDto> Buckets,
    ActivitySummaryDto Summary);
