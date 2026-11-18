namespace Freitty.Application.Dashboard;

// How many buckets the chart shows for a period, the current one included.
public static class ActivityBuckets
{
    public static int CountOf(ActivityPeriod period)
    {
        return period switch
        {
            ActivityPeriod.Day => 14,
            ActivityPeriod.Week => 12,
            ActivityPeriod.Month => 6,
            ActivityPeriod.Quarter => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "The activity period is not known."),
        };
    }
}
