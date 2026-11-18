using Freitty.Application.Dashboard;
using Freitty.Application.Dashboard.GetDashboard;
using Freitty.Application.Me.GetMe;
using Freitty.Application.Orders.CountOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Dashboard;

public sealed class NeedAttentionTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly DateTimeOffset Invoiced = Toronto.At(2026, 4, 1, 12);

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task AwaitingActionCountsDraftsAndOrdersWithAnUnpaidInvoice()
    {
        Order unpaid = api.Orders.Completed();
        Order paid = api.Orders.Completed();
        Order twiceUnpaid = api.Orders.Completed();
        await api.SaveAsync(
            api.Orders.Draft(),
            unpaid,
            OrderBuilder.IssueInvoice(unpaid, 100.00m, Invoiced, paid: false),
            paid,
            OrderBuilder.IssueInvoice(paid, 100.00m, Invoiced, paid: true),
            twiceUnpaid,
            OrderBuilder.IssueInvoice(twiceUnpaid, 100.00m, Invoiced, paid: false),
            OrderBuilder.IssueInvoice(twiceUnpaid, 50.00m, Invoiced.AddDays(1), paid: false));

        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equivalent(new NeedAttentionDto(3, 3, 0, []), needAttention, strict: true);
    }

    [Fact]
    public async Task AnOrderWithTwoOpenAlertsIsCountedOnceAndListedWithBothTypes()
    {
        Order bothAlerts = api.Orders.Ready();
        OrderBuilder.Unload(bothAlerts, bothAlerts.Shipments.Single(), OrderBuilder.DeclaredStandard + 2, photos: 0);
        Order missingPhoto = api.Orders.Ready();
        OrderBuilder.Unload(missingPhoto, missingPhoto.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        Order resolved = api.Orders.Ready();
        OrderBuilder.Unload(resolved, resolved.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        OrderBuilder.ResolveAlerts(resolved);
        await api.SaveAsync(bothAlerts, missingPhoto, resolved);

        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equal(2, needAttention.Alerts);
        AlertOrderDto listed = Assert.Single(needAttention.AlertOrders, order => order.Number == bothAlerts.Number);
        Assert.Equal([AlertType.QuantityDelta, AlertType.MissingPhoto], listed.AlertTypes);
    }

    [Fact]
    public async Task AlertOrdersAreTheThreeWithTheNewestOpenAlert()
    {
        Order alertedFirst = WithMissingPhoto(Toronto.At(2026, 3, 10, 9));
        Order alertedSecond = WithMissingPhoto(Toronto.At(2026, 3, 11, 9));
        Order alertedThird = WithMissingPhoto(Toronto.At(2026, 3, 12, 9));
        Order alertedAgainLast = WithMissingPhoto(Toronto.At(2026, 3, 9, 9));
        OrderBuilder.Unload(
            alertedAgainLast,
            alertedAgainLast.Shipments.Single(),
            1,
            at: Toronto.At(2026, 3, 13, 9));
        await api.SaveAsync(alertedFirst, alertedSecond, alertedThird, alertedAgainLast);

        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equal(4, needAttention.Alerts);
        Assert.Equal(
            [alertedAgainLast.Number, alertedThird.Number, alertedSecond.Number],
            needAttention.AlertOrders.Select(order => order.Number));
    }

    [Fact]
    public async Task AlertOrdersAlertedAtTheSameInstantAreOrderedByNumber()
    {
        DateTimeOffset alertedAt = Toronto.At(2026, 3, 10, 9);
        Order[] orders = [WithMissingPhoto(alertedAt), WithMissingPhoto(alertedAt), WithMissingPhoto(alertedAt)];
        await api.SaveAsync(orders);

        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equal(
            orders.Select(order => order.Number).Order(StringComparer.Ordinal),
            needAttention.AlertOrders.Select(order => order.Number));
    }

    [Fact]
    public async Task AnOrderWithAnOpenAlertAndAnUnpaidInvoiceIsCountedInBoth()
    {
        Order order = WithMissingPhoto(Toronto.At(2026, 3, 10, 9));
        await api.SaveAsync(order, OrderBuilder.IssueInvoice(order, 100.00m, Invoiced, paid: false));

        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equal((2, 1, 1), (needAttention.Total, needAttention.AwaitingAction, needAttention.Alerts));
    }

    [Fact]
    public async Task AlertsEqualTheAlertsCounterOfTheOrderList()
    {
        await api.SaveAsync(
            WithMissingPhoto(Toronto.At(2026, 3, 10, 9)),
            WithMissingPhoto(Toronto.At(2026, 3, 11, 9)),
            api.Orders.Ready());

        NeedAttentionDto needAttention = await NeedAttentionAsync();
        OrderCountsDto counts = await api.GetAsync<OrderCountsDto>(ApiUrls.OrderCounts);

        Assert.Equal(counts.Alerts, needAttention.Alerts);
    }

    [Fact]
    public async Task MeCarriesTheNeedAttentionTotalOfTheDashboard()
    {
        Order unpaid = api.Orders.Completed();
        await api.SaveAsync(
            api.Orders.Draft(),
            unpaid,
            OrderBuilder.IssueInvoice(unpaid, 100.00m, Invoiced, paid: false),
            WithMissingPhoto(Toronto.At(2026, 3, 10, 9)));

        MeDto me = await api.GetAsync<MeDto>(ApiUrls.Me);
        NeedAttentionDto needAttention = await NeedAttentionAsync();

        Assert.Equal(3, needAttention.Total);
        Assert.Equal(needAttention.Total, me.NeedAttentionCount);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private Order WithMissingPhoto(DateTimeOffset alertedAt)
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0, at: alertedAt);
        return order;
    }

    private async Task<NeedAttentionDto> NeedAttentionAsync()
    {
        DashboardDto dashboard = await api.GetAsync<DashboardDto>(ApiUrls.Dashboard);
        return dashboard.NeedAttention;
    }
}
