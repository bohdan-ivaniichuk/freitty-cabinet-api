using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Orders.CountOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderCountsTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task CountersCountEachTab()
    {
        await SaveMixedOrdersAsync();

        OrderCountsDto counts = await CountAsync(string.Empty);

        Assert.Equal(new OrderCountsDto(All: 7, CrossDock: 4, Consolidation: 3, Alerts: 2, Drafts: 2), counts);
    }

    [Fact]
    public async Task DraftsCounterIgnoresTheStatusFilter()
    {
        await SaveMixedOrdersAsync();

        OrderCountsDto counts = await CountAsync("status=Ready");

        Assert.Equal(new OrderCountsDto(All: 1, CrossDock: 1, Consolidation: 0, Alerts: 0, Drafts: 2), counts);
    }

    [Fact]
    public async Task EachCounterEqualsTheTotalOfTheListUnderTheSameFilters()
    {
        await SaveMixedOrdersAsync();
        string[] hubs = [string.Empty, $"hub={api.Data.OtherHub.Id}"];
        string[] statuses = [string.Empty, "status=Draft", "status=InProgress"];
        string[] dates = [string.Empty, "date=ThisWeek"];
        string[] searches = [string.Empty, "q=REF"];

        foreach (string filters in Combine(hubs, statuses, dates, searches))
        {
            OrderCountsDto counts = await CountAsync(filters);
            string withoutStatus = string.Join('&', filters.Split('&').Where(part => !part.StartsWith("status=")));

            OrderCountsDto totals = new(
                await TotalAsync(filters),
                await TotalAsync($"{filters}&type=CrossDock"),
                await TotalAsync($"{filters}&type=Consolidation"),
                await TotalAsync($"{filters}&alerts=true"),
                await TotalAsync($"{withoutStatus}&status=Draft"));
            Assert.True(counts == totals, $"With '{filters}' the counters are {counts}, the list totals {totals}.");
        }
    }

    [Fact]
    public async Task OrdersOfAnotherCompanyAreNotCounted()
    {
        TestData otherCompany = TestData.Create();
        var foreignOrders = new OrderBuilder(otherCompany);
        Order foreignWithAlert = foreignOrders.Ready();
        OrderBuilder.Unload(foreignWithAlert, foreignWithAlert.Shipments.Single(), 1, photos: 0);
        await api.SaveAsync([.. otherCompany.Entities(), foreignOrders.Draft(), foreignWithAlert, api.Orders.Ready()]);

        OrderCountsDto counts = await CountAsync(string.Empty);

        Assert.Equal(new OrderCountsDto(All: 1, CrossDock: 1, Consolidation: 0, Alerts: 0, Drafts: 0), counts);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    // Two drafts without shipments, so a search for "REF" leaves them out; orders in every other status at both
    // hubs; two with an open alert. Two orders are scheduled this week, the others next week or not at all.
    private async Task SaveMixedOrdersAsync()
    {
        DateTimeOffset nextWeek = CabinetApi.Now.AddDays(7);
        Order crossDockWithAlert = api.Orders.Ready(scheduledAt: nextWeek, hub: api.Data.OtherHub);
        OrderBuilder.Unload(crossDockWithAlert, crossDockWithAlert.Shipments.Single(), 1, photos: 0);
        Order consolidationWithAlert = api.Orders.Ready(OrderType.Consolidation, scheduledAt: CabinetApi.Now);
        OrderBuilder.Unload(consolidationWithAlert, consolidationWithAlert.Shipments.First(), 1, photos: 0);

        await api.SaveAsync(
            api.Orders.Draft(),
            api.Orders.Draft(OrderType.Consolidation, api.Data.OtherHub),
            api.Orders.Ready(scheduledAt: CabinetApi.Now),
            api.Orders.Received(OrderType.Consolidation, scheduledAt: nextWeek),
            api.Orders.Completed(scheduledAt: nextWeek),
            crossDockWithAlert,
            consolidationWithAlert);
    }

    private static IEnumerable<string> Combine(params string[][] choices)
    {
        IEnumerable<string> combinations = [string.Empty];
        foreach (string[] choice in choices)
        {
            combinations = combinations.SelectMany(prefix => choice.Select(part => Join(prefix, part))).ToList();
        }

        return combinations;
    }

    private static string Join(string first, string second)
    {
        return string.Join('&', new[] { first, second }.Where(part => part.Length > 0));
    }

    private Task<OrderCountsDto> CountAsync(string filters)
    {
        return api.GetAsync<OrderCountsDto>($"/api/v1/orders/counts?{filters}");
    }

    private async Task<int> TotalAsync(string filters)
    {
        return (await api.ListAsync($"?{filters}")).Total;
    }
}
