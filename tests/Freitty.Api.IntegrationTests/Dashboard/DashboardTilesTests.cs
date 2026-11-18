using Freitty.Api.IntegrationTests.Orders;
using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Dashboard.GetDashboard;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Dashboard;

// The API's clock stands at 12:00 on Wednesday 15 April 2026 in Toronto: the last 30 days begin at 16:00 UTC on
// 16 March, the 30 before them at 16:00 UTC on 14 February, and the week on Monday 13 April.
public sealed class DashboardTilesTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly DateTimeOffset Last30DaysFrom = CabinetApi.Now.AddDays(-30);

    // Late enough for both windows to begin after the instant the test orders are built at.
    private static readonly DateTimeOffset LaterNow = Toronto.At(2026, 6, 15, 12);

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task CompletedOrdersFallIntoTheRollingWindows()
    {
        await api.SaveAsync(
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 10, 12)),
            api.Orders.Completed(completedAt: Last30DaysFrom.AddSeconds(-1)),
            api.Orders.Completed(completedAt: Last30DaysFrom),
            api.Orders.Completed(completedAt: CabinetApi.Now.AddHours(-1)));

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(new CompletedOrdersDto(Last30Days: 2, Previous30Days: 2), dashboard.Completed);
        Assert.Equal(2, dashboard.Activity.Summary.CompletedLast30Days);
        Assert.Equal(0, dashboard.Activity.Summary.CompletedChangePercent);
    }

    [Fact]
    public async Task PreviousWindowStartsExactlySixtyDaysBeforeNow()
    {
        await using var laterApi = new CabinetApi(postgres.ConnectionString, LaterNow);
        DateTimeOffset previous30DaysFrom = LaterNow.AddDays(-60);
        await laterApi.SaveAsync(
            laterApi.Orders.Completed(completedAt: previous30DaysFrom),
            laterApi.Orders.Completed(completedAt: previous30DaysFrom.AddSeconds(-1)));

        DashboardDto dashboard = await laterApi.GetAsync<DashboardDto>("/api/v1/dashboard");

        Assert.Equal(new CompletedOrdersDto(Last30Days: 0, Previous30Days: 1), dashboard.Completed);
    }

    [Fact]
    public async Task SpendOfTheLast30DaysStartsExactlyThirtyDaysBeforeNow()
    {
        await using var laterApi = new CabinetApi(postgres.ConnectionString, LaterNow);
        DateTimeOffset last30DaysFrom = LaterNow.AddDays(-30);
        Order order = laterApi.Orders.Completed(completedAt: last30DaysFrom.AddDays(-1));
        await laterApi.SaveAsync(
            order,
            OrderBuilder.IssueInvoice(order, 100.00m, last30DaysFrom, paid: false),
            OrderBuilder.IssueInvoice(order, 7.00m, last30DaysFrom.AddSeconds(-1), paid: false));

        DashboardDto dashboard = await laterApi.GetAsync<DashboardDto>("/api/v1/dashboard");

        Assert.Equal(100.00m, dashboard.Activity.Summary.SpendLast30Days);
    }

    [Fact]
    public async Task ChangeThisWeekComparesWithTheOrdersActiveOnMonday()
    {
        DateTimeOffset thisTuesday = Toronto.At(2026, 4, 14, 9);
        await api.SaveAsync(
            api.Orders.Ready(),
            api.Orders.Received(),
            api.Orders.Ready(submittedAt: thisTuesday),
            api.Orders.Ready(submittedAt: thisTuesday),
            api.Orders.Completed(completedAt: thisTuesday),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 12, 23, 30)),
            api.Orders.Draft());

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(new ActiveOrdersDto(Count: 4, ChangeThisWeek: 1), dashboard.ActiveOrders);
    }

    [Fact]
    public async Task CompletedChangePercentIsNullWhenThePreviousWindowIsEmpty()
    {
        await api.SaveAsync(api.Orders.Completed(completedAt: Toronto.At(2026, 4, 1, 12)));

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(new CompletedOrdersDto(Last30Days: 1, Previous30Days: 0), dashboard.Completed);
        Assert.Null(dashboard.Activity.Summary.CompletedChangePercent);
    }

    [Fact]
    public async Task CompletedChangePercentIsRoundedToAWholeNumber()
    {
        await api.SaveAsync(
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 5, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 6, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 3, 7, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 1, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 2, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 3, 12)),
            api.Orders.Completed(completedAt: Toronto.At(2026, 4, 4, 12)));

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(33, dashboard.Activity.Summary.CompletedChangePercent);
    }

    [Fact]
    public async Task CardsAreTheFirstFourActiveOrdersInTheOrderOfTheList()
    {
        Order[] active = Enumerable.Range(1, 5)
            .Select(day => api.Orders.Ready(scheduledAt: Toronto.At(2026, 4, 15 + day, 9)))
            .ToArray();
        Order started = api.Orders.Received(scheduledAt: Toronto.At(2026, 4, 18, 12));
        await api.SaveAsync([
            .. active,
            started,
            api.Orders.Draft(),
            api.Orders.Completed(scheduledAt: Toronto.At(2026, 4, 30, 9)),
        ]);

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(
            [active[4].Number, active[3].Number, started.Number, active[2].Number],
            dashboard.Orders.Select(card => card.Number));
    }

    [Fact]
    public async Task OrdersOfAnotherCompanyChangeNothing()
    {
        TestData otherCompany = TestData.Create();
        var foreignOrders = new OrderBuilder(otherCompany);
        Order foreignCompleted = foreignOrders.Completed(completedAt: Toronto.At(2026, 4, 1, 12));
        Order foreignWithAlert = foreignOrders.Ready();
        OrderBuilder.Unload(foreignWithAlert, foreignWithAlert.Shipments.Single(), 1, photos: 0);
        await api.SaveAsync([
            .. otherCompany.Entities(),
            foreignCompleted,
            OrderBuilder.IssueInvoice(foreignCompleted, 100.00m, Toronto.At(2026, 4, 1, 13), paid: false),
            foreignWithAlert,
            foreignOrders.Draft(),
            foreignOrders.Ready(submittedAt: Toronto.At(2026, 4, 14, 9)),
        ]);

        DashboardDto dashboard = await DashboardAsync();

        Assert.Equal(new ActiveOrdersDto(0, 0), dashboard.ActiveOrders);
        Assert.Equal(new CompletedOrdersDto(0, 0), dashboard.Completed);
        Assert.Equivalent(new NeedAttentionDto(0, 0, 0, []), dashboard.NeedAttention, strict: true);
        Assert.Empty(dashboard.Orders);
        Assert.All(dashboard.Activity.Buckets, bucket => Assert.Equal(0, bucket.CompletedOrders));
        Assert.All(dashboard.Activity.Buckets, bucket => Assert.Equal(0m, bucket.Spend));
        Assert.Equal(new ActivitySummaryDto(0, null, 0m, null, null), dashboard.Activity.Summary);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private Task<DashboardDto> DashboardAsync()
    {
        return api.GetAsync<DashboardDto>("/api/v1/dashboard");
    }
}
