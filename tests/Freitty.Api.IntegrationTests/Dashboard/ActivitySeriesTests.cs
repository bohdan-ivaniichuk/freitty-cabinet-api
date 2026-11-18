using Freitty.Application.Dashboard;
using Freitty.Application.Dashboard.GetDashboard;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Dashboard;

// The twelve weeks of the default series end with the week of CabinetApi.Now: they begin on Monday 26 January and
// span the change to daylight saving time on Sunday 8 March.
public sealed class ActivitySeriesTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Theory]
    [InlineData("Day", 14, "2026-04-02", "2026-04-15")]
    [InlineData("Week", 12, "2026-01-26", "2026-04-13")]
    [InlineData("Month", 6, "2025-11-01", "2026-04-01")]
    [InlineData("Quarter", 4, "2025-07-01", "2026-04-01")]
    public async Task EachPeriodHasItsBucketsOldestFirstEndingWithTheCurrentOne(
        string period,
        int bucketCount,
        string firstStart,
        string lastStart)
    {
        await api.SaveReferenceDataAsync();

        ActivityDto activity = await ActivityAsync(period);

        Assert.Equal(Enum.Parse<ActivityPeriod>(period), activity.Period);
        Assert.Equal(bucketCount, activity.Buckets.Count);
        Assert.Equal(DateOnly.Parse(firstStart), activity.Buckets[0].Start);
        Assert.Equal(DateOnly.Parse(lastStart), activity.Buckets[^1].Start);
        Assert.Equal(activity.Buckets.OrderBy(bucket => bucket.Start), activity.Buckets);
    }

    [Fact]
    public async Task PeriodIsAWeekWhenLeftOut()
    {
        await api.SaveReferenceDataAsync();

        DashboardDto dashboard = await api.GetAsync<DashboardDto>(ApiUrls.Dashboard);

        Assert.Equal(ActivityPeriod.Week, dashboard.Activity.Period);
        Assert.Equal(12, dashboard.Activity.Buckets.Count);
    }

    [Fact]
    public async Task QuarterBucketsStartOnTheFirstDayOfAQuarter()
    {
        await using var midQuarterApi = new CabinetApi(postgres.ConnectionString, Toronto.At(2026, 5, 15, 12));
        await midQuarterApi.SaveReferenceDataAsync();

        DashboardDto dashboard = await midQuarterApi.GetAsync<DashboardDto>($"{ApiUrls.Dashboard}?period=Quarter");

        Assert.Equal(
            [new DateOnly(2025, 7, 1), new DateOnly(2025, 10, 1), new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 1)],
            dashboard.Activity.Buckets.Select(bucket => bucket.Start));
    }

    // Santiago moves its clocks forward at midnight: 6 September 2026 begins at 01:00 (UTC−3).
    [Fact]
    public async Task DaysAfterASkippedMidnightKeepTheirBuckets()
    {
        var now = new DateTimeOffset(2026, 9, 10, 15, 0, 0, TimeSpan.Zero);
        await using var santiagoApi = new CabinetApi(postgres.ConnectionString, now, "America/Santiago");
        var middayOnTheEighth = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.FromHours(-3));
        await santiagoApi.SaveAsync(santiagoApi.Orders.Completed(completedAt: middayOnTheEighth));

        DashboardDto dashboard = await santiagoApi.GetAsync<DashboardDto>($"{ApiUrls.Dashboard}?period=Day");

        IReadOnlyList<ActivityBucketDto> buckets = dashboard.Activity.Buckets;
        Assert.Equal(
            Enumerable.Range(0, 14).Select(day => new DateOnly(2026, 8, 28).AddDays(day)),
            buckets.Select(bucket => bucket.Start));
        Assert.Equal(1, buckets.Single(bucket => bucket.Start == new DateOnly(2026, 9, 8)).CompletedOrders);
    }

    [Fact]
    public async Task EmptyBucketsArePartOfTheSeries()
    {
        await api.SaveAsync(
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 5, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 8, 12)));

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(12, activity.Buckets.Count);
        Assert.Equal(
            [new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 6)],
            activity.Buckets.Where(bucket => bucket.CompletedOrders > 0).Select(bucket => bucket.Start));
        Assert.Equal(new ActivityBucketDto(new DateOnly(2026, 3, 16), 0, 0m), activity.Buckets[7]);
    }

    [Fact]
    public async Task WeeksAcrossADaylightSavingChangeStartOnMondayAndKeepTheirOrders()
    {
        await api.SaveAsync(
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 5, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 10, 12)));

        ActivityDto activity = await ActivityAsync("Week");

        Assert.All(activity.Buckets, bucket => Assert.Equal(DayOfWeek.Monday, bucket.Start.DayOfWeek));
        Assert.All(
            activity.Buckets.Skip(1).Zip(activity.Buckets),
            pair => Assert.Equal(pair.Second.Start.AddDays(7), pair.First.Start));
        Assert.Equal(1, CompletedIn(activity, new DateOnly(2026, 3, 2)));
        Assert.Equal(1, CompletedIn(activity, new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public async Task WeeksAreCutAtMidnightInTheReportingZone()
    {
        DateTimeOffset lateOnSunday = Toronto.At(2026, 3, 29, 23, 30);
        DateTimeOffset earlyOnMonday = Toronto.At(2026, 3, 30, 0, 30);
        Assert.Equal(lateOnSunday.UtcDateTime.Date, earlyOnMonday.UtcDateTime.Date);
        await api.SaveAsync(
            api.Orders.Completed(completedAt: lateOnSunday),
            api.Orders.Completed(completedAt: earlyOnMonday));

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(1, CompletedIn(activity, new DateOnly(2026, 3, 23)));
        Assert.Equal(1, CompletedIn(activity, new DateOnly(2026, 3, 30)));
    }

    [Fact]
    public async Task SpendOfABucketIsTheSumOfTheInvoicesIssuedInIt()
    {
        await SaveInvoicedOrdersAsync();

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(new ActivityBucketDto(new DateOnly(2026, 3, 9), 1, 80.00m), activity.Buckets[6]);
        Assert.Equal(new ActivityBucketDto(new DateOnly(2026, 3, 30), 1, 150.50m), activity.Buckets[9]);
        Assert.Equal(new ActivityBucketDto(new DateOnly(2026, 4, 6), 2, 200.00m), activity.Buckets[10]);
    }

    [Fact]
    public async Task SpendOfTheLast30DaysCountsTheInvoicesIssuedInThem()
    {
        await SaveInvoicedOrdersAsync();

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(350.50m, activity.Summary.SpendLast30Days);
    }

    [Fact]
    public async Task AverageSpendIsPerOrderInvoicedInTheLast30Days()
    {
        await SaveInvoicedOrdersAsync();

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(175.25m, activity.Summary.AverageSpendPerOrder);
    }

    [Fact]
    public async Task BestBucketIsTheOneWithTheHighestSpend()
    {
        await SaveInvoicedOrdersAsync();

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(new ActivityBucketDto(new DateOnly(2026, 4, 6), 2, 200.00m), activity.Summary.BestBucket);
    }

    [Fact]
    public async Task BestBucketIsTheLaterOneOnATie()
    {
        Order earlier = api.Orders.Completed(completedAt: Toronto.At(2026, 4, 1, 12));
        Order later = api.Orders.Completed(completedAt: Toronto.At(2026, 4, 8, 12));
        await api.SaveAsync(
            earlier,
            OrderBuilder.IssueInvoice(earlier, 100.00m, Toronto.At(2026, 4, 1, 13), paid: true),
            later,
            OrderBuilder.IssueInvoice(later, 100.00m, Toronto.At(2026, 4, 8, 13), paid: true));

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(new DateOnly(2026, 4, 6), activity.Summary.BestBucket?.Start);
    }

    [Fact]
    public async Task BestBucketAndAverageAreNullWithoutSpend()
    {
        await api.SaveAsync(api.Orders.Completed(completedAt: Toronto.At(2026, 4, 1, 12)));

        ActivityDto activity = await ActivityAsync("Week");

        Assert.Equal(new ActivitySummaryDto(1, null, 0m, null, null), activity.Summary);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    // Week of 9 March: one order, 80.00. Week of 30 March: one order invoiced twice, 150.50, the second invoice late
    // on Sunday. Week of 6 April: two orders, one invoiced 200.00. Only the last two weeks are in the last 30 days.
    private async Task SaveInvoicedOrdersAsync()
    {
        Order invoicedEarly = api.Orders.Completed(completedAt: Toronto.At(2026, 3, 10, 12));
        Order invoicedTwice = api.Orders.Completed(completedAt: Toronto.At(2026, 4, 1, 12));
        Order invoicedOnce = api.Orders.Completed(completedAt: Toronto.At(2026, 4, 8, 12));
        await api.SaveAsync(
            invoicedEarly,
            OrderBuilder.IssueInvoice(invoicedEarly, 80.00m, Toronto.At(2026, 3, 10, 13), paid: true),
            invoicedTwice,
            OrderBuilder.IssueInvoice(invoicedTwice, 100.00m, Toronto.At(2026, 4, 1, 13), paid: false),
            OrderBuilder.IssueInvoice(invoicedTwice, 50.50m, Toronto.At(2026, 4, 5, 23, 30), paid: true),
            invoicedOnce,
            OrderBuilder.IssueInvoice(invoicedOnce, 200.00m, Toronto.At(2026, 4, 8, 13), paid: false),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 9, 12)));
    }

    private static int CompletedIn(ActivityDto activity, DateOnly start)
    {
        return activity.Buckets.Single(bucket => bucket.Start == start).CompletedOrders;
    }

    private async Task<ActivityDto> ActivityAsync(string period)
    {
        DashboardDto dashboard = await api.GetAsync<DashboardDto>($"{ApiUrls.Dashboard}?period={period}");
        return dashboard.Activity;
    }
}
