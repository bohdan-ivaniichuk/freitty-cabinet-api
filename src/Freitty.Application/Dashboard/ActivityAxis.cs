namespace Freitty.Application.Dashboard;

// How a period cuts the time axis of the chart: the calendar unit a bucket starts on, the length of one bucket and
// the number of buckets up to and including the current one. Unit and step are written as PostgreSQL reads them
// (date_trunc's unit, an interval); a quarter is stepped as three months.
public sealed record ActivityAxis(string Unit, string Step, int Buckets)
{
    public static ActivityAxis Of(ActivityPeriod period)
    {
        return period switch
        {
            ActivityPeriod.Day => new ActivityAxis("day", "1 day", Buckets: 14),
            ActivityPeriod.Week => new ActivityAxis("week", "1 week", Buckets: 12),
            ActivityPeriod.Month => new ActivityAxis("month", "1 month", Buckets: 6),
            ActivityPeriod.Quarter => new ActivityAxis("quarter", "3 months", Buckets: 4),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "The activity period is not known."),
        };
    }
}
