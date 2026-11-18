namespace Freitty.Application.Dashboard;

// Start is the local date, in the reporting zone, on which the bucket begins.
public sealed record ActivityBucketDto(DateOnly Start, int CompletedOrders, decimal Spend);
