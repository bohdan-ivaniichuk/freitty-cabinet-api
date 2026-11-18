using Freitty.Application.Orders.GetPipeline;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class PipelineTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const int ColumnSize = GetPipelineQuery.PipelineColumnSize;
    private const string OtherHubPlaceholder = "{otherHub}";

    private static readonly OrderStatus[] StatusesInOrder =
        [OrderStatus.Draft, OrderStatus.Ready, OrderStatus.InProgress, OrderStatus.Completed];

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task BoardHasAColumnForEveryStatusInOrderWithItsTotal()
    {
        await api.SaveAsync(
            api.Orders.Draft(),
            api.Orders.Ready(),
            api.Orders.Ready(),
            api.Orders.Completed());

        PipelineDto board = await api.PipelineAsync();

        Assert.Equal(StatusesInOrder, board.Columns.Select(column => column.Status));
        Assert.Equal([1, 2, 0, 1], board.Columns.Select(column => column.Total));
        Assert.All(board.Columns, column => Assert.Equal(column.Total, column.Orders.Count));
        Assert.All(
            board.Columns,
            column => Assert.All(column.Orders, card => Assert.Equal(column.Status, card.Status)));
    }

    // Seven Ready orders on four days, so some share a Scheduled date and the id decides: the column is the first
    // page of the list filtered to its status.
    [Fact]
    public async Task ColumnHoldsTheFirstOrdersOfItsStatusInTheOrderOfTheList()
    {
        Order[] ready = Enumerable.Range(1, ColumnSize + 2)
            .Select(day => api.Orders.Ready(scheduledAt: CabinetApi.Now.AddDays(day % 4)))
            .ToArray();
        await api.SaveAsync([.. ready, api.Orders.Draft()]);

        PipelineColumnDto column = ColumnOf(await api.PipelineAsync(), OrderStatus.Ready);

        Assert.Equal(ColumnSize + 2, column.Total);
        Assert.Equal(
            await api.ListNumbersAsync($"?status=Ready&pageSize={ColumnSize}"),
            column.Orders.Select(card => card.Number));
    }

    // The other hub's id is known only once the test has its data, so the case names it with a placeholder.
    [Theory]
    [InlineData("")]
    [InlineData("type=Consolidation")]
    [InlineData("hasOpenAlerts=true")]
    [InlineData("hubId=" + OtherHubPlaceholder)]
    [InlineData("datePreset=ThisWeek")]
    [InlineData("search=REF")]
    public async Task EachColumnIsTheListUnderTheSameFilterAndItsStatus(string filterCase)
    {
        await SaveMixedOrdersAsync();
        string filter = filterCase.Replace(
            OtherHubPlaceholder,
            api.Data.OtherHub.Id.ToString(),
            StringComparison.Ordinal);

        PipelineDto board = await api.PipelineAsync($"?{filter}");

        foreach (PipelineColumnDto column in board.Columns)
        {
            string listQuery = $"?{filter}&status={column.Status}&pageSize={ColumnSize}";
            Assert.True(
                column.Total == (await api.ListAsync(listQuery)).Total,
                $"The {column.Status} column counts {column.Total}.");
            Assert.Equal(await api.ListNumbersAsync(listQuery), column.Orders.Select(card => card.Number));
        }
    }

    [Fact]
    public async Task StatusFilterLeavesTheOtherColumnsEmpty()
    {
        await SaveMixedOrdersAsync();

        PipelineDto board = await api.PipelineAsync("?status=Ready");

        Assert.Equal(StatusesInOrder, board.Columns.Select(column => column.Status));
        Assert.Equal([0, 2, 0, 0], board.Columns.Select(column => column.Total));
        Assert.All(
            board.Columns.Where(column => column.Status != OrderStatus.Ready),
            column => Assert.Empty(column.Orders));
    }

    [Fact]
    public async Task BoardWithoutOrdersHasFourEmptyColumns()
    {
        await api.SaveReferenceDataAsync();

        PipelineDto board = await api.PipelineAsync();

        Assert.Equal(StatusesInOrder, board.Columns.Select(column => column.Status));
        Assert.All(board.Columns, column => Assert.Equal((0, 0), (column.Total, column.Orders.Count)));
    }

    [Fact]
    public async Task OrdersOfAnotherCompanyAreNotOnTheBoard()
    {
        TestData otherCompany = TestData.Create();
        Order own = api.Orders.Ready();
        await api.SaveAsync([.. otherCompany.Entities(), new OrderBuilder(otherCompany).Ready(), own]);

        PipelineDto board = await api.PipelineAsync();

        Assert.Equal([0, 1, 0, 0], board.Columns.Select(column => column.Total));
        Assert.Equal(own.Number, Assert.Single(ColumnOf(board, OrderStatus.Ready).Orders).Number);
    }

    [Fact]
    public async Task CardCarriesTheVersionOfItsOrder()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        PipelineDto board = await api.PipelineAsync();

        Assert.Equal(draft.Version, Assert.Single(ColumnOf(board, OrderStatus.Draft).Orders).Version);
    }

    [Theory]
    [InlineData("type=Pallet", "type")]
    [InlineData("status=Done", "status")]
    [InlineData("datePreset=Yesterday", "datePreset")]
    [InlineData("hubId=Markham", "hubId")]
    [InlineData("hubId=", "hubId")]
    [InlineData("hasOpenAlerts=maybe", "hasOpenAlerts")]
    [InlineData("search=ab", "search")]
    [InlineData("search=ab%00c", "search")]
    public async Task InvalidParameterIsABadRequestNamingTheField(string query, string field)
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.OrderPipeline}?{query}");

        await ProblemResponse.AssertValidationProblemAsync(response, field);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static PipelineColumnDto ColumnOf(PipelineDto board, OrderStatus status)
    {
        return board.Columns.Single(column => column.Status == status);
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
            api.Orders.Ready(OrderType.Consolidation, scheduledAt: nextWeek),
            api.Orders.Received(OrderType.Consolidation, scheduledAt: nextWeek),
            api.Orders.Completed(scheduledAt: nextWeek),
            crossDockWithAlert,
            consolidationWithAlert);
    }
}
