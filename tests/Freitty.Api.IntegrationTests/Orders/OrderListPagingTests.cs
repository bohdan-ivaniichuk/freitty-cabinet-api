using Freitty.Application.Orders.ListOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderListPagingTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const int OrderCount = 5;
    private const int PageSize = 2;

    private static readonly DateTimeOffset SharedScheduledDate = new(2026, 4, 20, 14, 0, 0, TimeSpan.Zero);

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task PagesSplitTheListWithTheTotalOnEachPage()
    {
        await SaveOrdersOnOneDateAsync();

        OrderPageDto[] pages = await ReadPagesAsync(1, 2, 3);

        Assert.Equal([2, 2, 1], pages.Select(page => page.Items.Count));
        Assert.All(pages, page => Assert.Equal(OrderCount, page.Total));
        Assert.All(pages, page => Assert.Equal(PageSize, page.PageSize));
        Assert.Equal([1, 2, 3], pages.Select(page => page.Page));
    }

    [Fact]
    public async Task PagePastTheEndIsEmpty()
    {
        await SaveOrdersOnOneDateAsync();

        OrderPageDto page = await api.ListAsync($"?page=4&pageSize={PageSize}");

        Assert.Empty(page.Items);
        Assert.Equal(OrderCount, page.Total);
    }

    // PostgreSQL orders uuids byte by byte, which is the order of their text.
    [Fact]
    public async Task OrdersSharingAScheduledDateArePagedByIdDescending()
    {
        Order[] orders = await SaveOrdersOnOneDateAsync();

        OrderPageDto[] pages = await ReadPagesAsync(1, 2, 3);

        IEnumerable<string> byIdDescending = orders
            .OrderByDescending(order => order.Id.ToString(), StringComparer.Ordinal)
            .Select(order => order.Number);
        Assert.Equal(byIdDescending, pages.SelectMany(page => page.Items).Select(card => card.Number));
    }

    [Fact]
    public async Task DefaultSortPutsUnscheduledOrdersFirstThenNewestScheduledDate()
    {
        Order early = api.Orders.Ready(scheduledAt: SharedScheduledDate.AddDays(-5));
        Order late = api.Orders.Ready(scheduledAt: SharedScheduledDate.AddDays(5));
        Order unscheduled = api.Orders.Draft();
        Order middle = api.Orders.Ready(scheduledAt: SharedScheduledDate);
        await api.SaveAsync(early, late, unscheduled, middle);

        string[] numbers = await api.ListNumbersAsync();

        Assert.Equal([unscheduled.Number, late.Number, middle.Number, early.Number], numbers);
    }

    [Fact]
    public async Task FirstPageOfTwelveIsTheDefault()
    {
        await api.SaveAsync(api.Orders.Ready());

        OrderPageDto page = await api.ListAsync();

        Assert.Equal(1, page.Page);
        Assert.Equal(ListOrdersQuery.DefaultPageSize, page.PageSize);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private async Task<Order[]> SaveOrdersOnOneDateAsync()
    {
        Order[] orders = Enumerable.Range(0, OrderCount)
            .Select(_ => api.Orders.Ready(scheduledAt: SharedScheduledDate))
            .ToArray();
        await api.SaveAsync(orders);
        return orders;
    }

    private async Task<OrderPageDto[]> ReadPagesAsync(params int[] pageNumbers)
    {
        var pages = new List<OrderPageDto>();
        foreach (int pageNumber in pageNumbers)
        {
            pages.Add(await api.ListAsync($"?page={pageNumber}&pageSize={PageSize}"));
        }

        return pages.ToArray();
    }
}
