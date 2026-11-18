namespace Freitty.Application.Dashboard.GetDashboard;

// Start is the local date, in the reporting zone, on which the bucket begins.
public sealed record ActivityBucketDto(DateOnly Start, int CompletedOrders, decimal Spend);
