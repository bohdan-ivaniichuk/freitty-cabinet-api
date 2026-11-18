using Freitty.Application.Orders.CountOrders;
using Freitty.Application.Orders.GetPipeline;
using Freitty.Application.Orders.ListOrders;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Seed;

// The Order List of the seeded company, as its tabs and its board show it.
[Collection(SeededDatabaseCollection.Name)]
public sealed class SeededOrderListTests(SeededDatabase seeded)
{
    [Fact]
    public async Task TabCountersCountTheSeededOrders()
    {
        OrderCountsDto counts = await seeded.GetAsync<OrderCountsDto>(ApiUrls.OrderCounts);

        Assert.Equal(new OrderCountsDto(All: 71, CrossDock: 52, Consolidation: 19, Alerts: 2, Drafts: 2), counts);
    }

    [Fact]
    public async Task BoardHasTheSeededOrdersOfEveryStatusInTheOrderOfTheList()
    {
        PipelineDto board = await seeded.GetAsync<PipelineDto>(ApiUrls.OrderPipeline);

        Assert.Equal(
            [(OrderStatus.Draft, 2), (OrderStatus.Ready, 2), (OrderStatus.InProgress, 5), (OrderStatus.Completed, 62)],
            board.Columns.Select(column => (column.Status, column.Total)));
        foreach (PipelineColumnDto column in board.Columns)
        {
            OrderPageDto page = await seeded.GetAsync<OrderPageDto>(
                $"{ApiUrls.Orders}?status={column.Status}&pageSize={GetPipelineQuery.PipelineColumnSize}");
            Assert.Equal(page.Items.Select(card => card.Number), column.Orders.Select(card => card.Number));
        }
    }
}
