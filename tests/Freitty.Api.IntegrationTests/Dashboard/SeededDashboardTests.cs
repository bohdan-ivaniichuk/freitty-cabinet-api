using Freitty.Application.Dashboard;
using Freitty.Application.Dashboard.GetDashboard;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Dashboard;

// The dashboard of the seeded company on the day it was seeded: Saturday 18 April 2026, 09:00 in Toronto.
[Collection(SeededDatabaseCollection.Name)]
public sealed class SeededDashboardTests(SeededDatabase seeded)
{
    [Fact]
    public async Task TilesShowTheSeededOrders()
    {
        DashboardDto dashboard = await DashboardAsync("Week");

        Assert.Equal(7, dashboard.ActiveOrders.Count);
        Assert.Equal(new CompletedOrdersDto(Last30Days: 24, Previous30Days: 20), dashboard.Completed);
        Assert.Equal(20, dashboard.Activity.Summary.CompletedChangePercent);
    }

    [Fact]
    public async Task NeedAttentionIsTheTwoDraftsTheUnpaidInvoiceAndTheTwoAlertedOrders()
    {
        NeedAttentionDto needAttention = (await DashboardAsync("Week")).NeedAttention;

        Assert.Equal((5, 3, 2), (needAttention.Total, needAttention.AwaitingAction, needAttention.Alerts));
        Assert.Equal(["FR001383", "FR001674"], needAttention.AlertOrders.Select(order => order.Number));
    }

    [Fact]
    public async Task CardsAreTheFourActiveOrdersScheduledLast()
    {
        DashboardDto dashboard = await DashboardAsync("Week");

        Assert.Equal(
            ["FR001681", "FR001679", "FR001678", "FR001383"],
            dashboard.Orders.Select(card => card.Number));
    }

    // Saturday's current week holds the anchor day and the day before it; on a Monday it would be empty.
    [Fact]
    public async Task OnASaturdayEveryWeekHasCompletedOrders()
    {
        DashboardDto dashboard = await DashboardAsync("Week");

        IReadOnlyList<ActivityBucketDto> buckets = dashboard.Activity.Buckets;
        Assert.Equal(12, buckets.Count);
        Assert.Equal(new DateOnly(2026, 4, 13), buckets[^1].Start);
        Assert.All(buckets, bucket => Assert.True(bucket.CompletedOrders > 0, $"{bucket}"));
    }

    // The right edge of the chart: the seeding day itself is still empty, the two days before it are not.
    [Fact]
    public async Task TheAnchorDayAndTheDayBeforeItHaveCompletedOrdersAndSpend()
    {
        DashboardDto dashboard = await DashboardAsync("Day");

        IReadOnlyList<ActivityBucketDto> buckets = dashboard.Activity.Buckets;
        Assert.Equal(
            [new DateOnly(2026, 4, 16), new DateOnly(2026, 4, 17)],
            buckets.SkipLast(1).TakeLast(2).Select(bucket => bucket.Start));
        Assert.All(
            buckets.SkipLast(1).TakeLast(2),
            bucket => Assert.True(bucket.CompletedOrders > 0 && bucket.Spend > 0, $"{bucket}"));
    }

    [Fact]
    public async Task QuarterlySpendAddsUpToEveryInvoice()
    {
        DashboardDto dashboard = await DashboardAsync("Quarter");
        decimal invoiced = await seeded.InContextAsync(db =>
            db.Invoices.SumAsync(invoice => invoice.Amount, TestContext.Current.CancellationToken));

        Assert.Equal(invoiced, dashboard.Activity.Buckets.Sum(bucket => bucket.Spend));
    }

    private Task<DashboardDto> DashboardAsync(string period)
    {
        return seeded.GetAsync<DashboardDto>($"{ApiUrls.Dashboard}?period={period}");
    }
}
