using System.Net.Http.Json;
using Freitty.Api.IntegrationTests.Seed;
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
    public async Task NeedAttentionIsTheDraftTheUnpaidInvoiceAndTheTwoAlertedOrders()
    {
        NeedAttentionDto needAttention = (await DashboardAsync("Week")).NeedAttention;

        Assert.Equal((4, 2, 2), (needAttention.Total, needAttention.AwaitingAction, needAttention.Alerts));
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

    [Fact]
    public async Task EveryWeekBeforeTheCurrentOneHasCompletedOrders()
    {
        DashboardDto dashboard = await DashboardAsync("Week");

        IReadOnlyList<ActivityBucketDto> buckets = dashboard.Activity.Buckets;
        Assert.Equal(12, buckets.Count);
        Assert.Equal(new DateOnly(2026, 4, 13), buckets[^1].Start);
        Assert.All(buckets.SkipLast(1), bucket => Assert.True(bucket.CompletedOrders > 0, $"{bucket}"));
    }

    [Fact]
    public async Task QuarterlySpendAddsUpToEveryInvoice()
    {
        DashboardDto dashboard = await DashboardAsync("Quarter");
        decimal invoiced = await seeded.InContextAsync(db =>
            db.Invoices.SumAsync(invoice => invoice.Amount, TestContext.Current.CancellationToken));

        Assert.Equal(invoiced, dashboard.Activity.Buckets.Sum(bucket => bucket.Spend));
    }

    private async Task<DashboardDto> DashboardAsync(string period)
    {
        using HttpClient client = seeded.CreateClient();
        return (await client.GetFromJsonAsync<DashboardDto>(
            $"/api/v1/dashboard?period={period}",
            ApiJson.Options,
            TestContext.Current.CancellationToken))!;
    }
}
