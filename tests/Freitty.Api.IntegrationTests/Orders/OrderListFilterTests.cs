using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Orders.ListOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderListFilterTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task AllTabListsEveryOrderDraftsIncluded()
    {
        Order draft = api.Orders.Draft();
        Order ready = api.Orders.Ready();
        Order completed = api.Orders.Completed(OrderType.Consolidation);
        await api.SaveAsync(draft, ready, completed);

        string[] numbers = await api.ListNumbersAsync();

        Assert.Equivalent(new[] { draft.Number, ready.Number, completed.Number }, numbers, strict: true);
    }

    [Theory]
    [InlineData(OrderType.CrossDock)]
    [InlineData(OrderType.Consolidation)]
    public async Task TypeTabListsOnlyOrdersOfThatTypeDraftsIncluded(OrderType type)
    {
        Order draft = api.Orders.Draft(type);
        Order ready = api.Orders.Ready(type);
        Order otherType = api.Orders.Ready(type == OrderType.CrossDock ? OrderType.Consolidation : OrderType.CrossDock);
        await api.SaveAsync(draft, ready, otherType);

        string[] numbers = await api.ListNumbersAsync($"?type={type}");

        Assert.Equivalent(new[] { draft.Number, ready.Number }, numbers, strict: true);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.InProgress)]
    [InlineData(OrderStatus.Completed)]
    public async Task StatusFilterListsOnlyOrdersInThatStatus(OrderStatus status)
    {
        Order[] orders = [api.Orders.Draft(), api.Orders.Ready(), api.Orders.Received(), api.Orders.Completed()];
        await api.SaveAsync(orders);

        string[] numbers = await api.ListNumbersAsync($"?status={status}");

        Assert.Equal(orders.Single(order => order.Status == status).Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task AlertsTabListsOrdersWithAnOpenAlertOnAnyShipment()
    {
        Order withAlert = api.Orders.Ready(OrderType.Consolidation);
        OrderBuilder.Unload(withAlert, withAlert.Shipments.Last(), OrderBuilder.DeclaredStandard, photos: 0);
        Order withResolvedAlert = api.Orders.Ready();
        OrderBuilder.Unload(withResolvedAlert, withResolvedAlert.Shipments.Single(), 1, photos: 0);
        OrderBuilder.ResolveAlerts(withResolvedAlert);
        Order withoutAlert = api.Orders.Received();
        await api.SaveAsync(withAlert, withResolvedAlert, withoutAlert);

        string[] numbers = await api.ListNumbersAsync("?alerts=true");

        Assert.Equal(withAlert.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task AlertsFalseIsTheSameAsNoAlertsFilter()
    {
        Order withAlert = api.Orders.Ready();
        OrderBuilder.Unload(withAlert, withAlert.Shipments.Single(), 1, photos: 0);
        Order withoutAlert = api.Orders.Ready();
        await api.SaveAsync(withAlert, withoutAlert);

        string[] numbers = await api.ListNumbersAsync("?alerts=false");

        Assert.Equal(2, numbers.Length);
    }

    [Fact]
    public async Task HubFilterMatchesTheHubThatHandlesTheOrderNotItsTransferHub()
    {
        Order atHub = api.Orders.Ready(hub: api.Data.Hub);
        TestData.Succeed(atHub.TransferTo(api.Data.OtherHub.Id, TestData.Now));
        Order atOtherHub = api.Orders.Ready(hub: api.Data.OtherHub);
        await api.SaveAsync(atHub, atOtherHub);

        string[] numbers = await api.ListNumbersAsync($"?hub={api.Data.OtherHub.Id}");

        Assert.Equal(atOtherHub.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task HubThatDoesNotExistListsNothing()
    {
        await api.SaveAsync(api.Orders.Ready());

        OrderPageDto page = await api.ListAsync($"?hub={Guid.NewGuid()}");

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task FiltersAndTabsCombine()
    {
        Order match = api.Orders.Ready(OrderType.Consolidation, hub: api.Data.OtherHub);
        OrderBuilder.Unload(match, match.Shipments.First(), 1, photos: 0);
        Order wrongHub = api.Orders.Ready(OrderType.Consolidation);
        OrderBuilder.Unload(wrongHub, wrongHub.Shipments.First(), 1, photos: 0);
        Order wrongType = api.Orders.Ready(hub: api.Data.OtherHub);
        OrderBuilder.Unload(wrongType, wrongType.Shipments.First(), 1, photos: 0);
        Order withoutAlert = api.Orders.Ready(OrderType.Consolidation, hub: api.Data.OtherHub);
        await api.SaveAsync(match, wrongHub, wrongType, withoutAlert);

        string[] numbers = await api.ListNumbersAsync(
            $"?type=Consolidation&status=InProgress&alerts=true&hub={api.Data.OtherHub.Id}");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task OrdersOfAnotherCompanyNeverAppear()
    {
        Order own = api.Orders.Ready();
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Ready(hub: api.Data.Hub);
        await api.SaveAsync([.. otherCompany.Entities(), own, foreign]);

        string[] numbers = await api.ListNumbersAsync($"?hub={api.Data.Hub.Id}");

        Assert.Equal(own.Number, Assert.Single(numbers));
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
