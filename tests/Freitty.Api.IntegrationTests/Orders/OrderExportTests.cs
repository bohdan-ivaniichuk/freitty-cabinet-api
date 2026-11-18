using System.Net;
using System.Text;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.ListOrders;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderExportTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string Header =
        "Number,Type,Status,Hub,Scheduled at (UTC),Destination,Carrier,Shipments,Declared,Actual,Open alerts," +
        "Created by";

    private const string LineEnd = "\r\n";
    private const string HubIdPlaceholder = "{hubId}";
    private const string SearchPlaceholder = "{search}";

    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    private readonly CabinetApi api = new(postgres.ConnectionString);

    // The query of the list, with the other hub's id and a part of one order's number put in at run time.
    public static TheoryData<string> FilterQueries =>
    [
        string.Empty,
        "type=CrossDock",
        "type=Consolidation",
        "status=Ready",
        "hasOpenAlerts=true",
        $"hubId={HubIdPlaceholder}",
        "datePreset=Today",
        "datePreset=ThisWeek",
        $"search={SearchPlaceholder}",
        "type=CrossDock&status=InProgress",
        "search=REF-&type=Consolidation",
        $"search={SearchPlaceholder}&hasOpenAlerts=true",
    ];

    [Fact]
    public async Task ExportIsTheHeaderAndARowWithEveryColumnOfAnOrder()
    {
        Order order = api.Orders.Ready(scheduledAt: CabinetApi.Now);
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard + 2, photos: 0);
        await api.SaveAsync(order);

        string[] lines = await ExportLinesAsync();

        Assert.Equal(
            [
                Header,
                $"{order.Number},CrossDock,InProgress,{api.Data.Hub.Name},2026-04-15T16:00:00Z," +
                $"\"{OrderBuilder.DestinationCity}, {OrderBuilder.DestinationRegion}\",{api.Data.Carrier.Name}," +
                "1,10,12,2,User 1",
            ],
            lines);
    }

    [Fact]
    public async Task UnscheduledDraftHasEmptyDateDestinationAndCarrier()
    {
        Order draft = api.Orders.Draft();
        await api.SaveAsync(draft);

        string[] lines = await ExportLinesAsync();

        Assert.Equal($"{draft.Number},CrossDock,Draft,{api.Data.Hub.Name},,,,0,0,0,0,User 1", lines[1]);
    }

    [Fact]
    public async Task CarrierColumnWordsAnOwnDriverAndASelfPickupAsTheCardsDo()
    {
        Order ownDriver = Submitted(order => order.ArrangeOwnDriver("User 5", OrderBuilder.BuiltAt));
        Order selfPickup = Submitted(order => order.ArrangeSelfPickup(OrderBuilder.BuiltAt));
        await api.SaveAsync(ownDriver, selfPickup);

        string[] lines = await ExportLinesAsync();

        Assert.Contains(RowOf(ownDriver, "Driver: User 5"), lines);
        Assert.Contains(RowOf(selfPickup, "Self pickup"), lines);
    }

    [Theory]
    [MemberData(nameof(FilterQueries))]
    public async Task ExportHasTheOrdersOfTheListInItsOrder(string query)
    {
        Order searched = await SaveOrdersOfEveryKindAsync();
        string resolvedQuery = query
            .Replace(HubIdPlaceholder, api.Data.OtherHub.Id.ToString(), StringComparison.Ordinal)
            .Replace(SearchPlaceholder, searched.Number[..10], StringComparison.Ordinal);

        string[] exported = await ExportNumbersAsync($"?{resolvedQuery}");

        string[] listed =
            await api.ListNumbersAsync($"?{resolvedQuery}&pageSize={ListOrdersQueryValidator.MaxPageSize}");
        Assert.NotEmpty(listed);
        Assert.Equal(listed, exported);
    }

    [Fact]
    public async Task ExportHasEveryOrderNotOnlyOnePage()
    {
        Order[] orders = Enumerable.Range(0, ListOrdersQueryValidator.MaxPageSize + 1)
            .Select(_ => api.Orders.Ready())
            .ToArray();
        await api.SaveAsync(orders);

        string[] exported = await ExportNumbersAsync();

        Assert.Equivalent(orders.Select(order => order.Number), exported, strict: true);
    }

    [Fact]
    public async Task OrdersOfAnotherCompanyNeverAppear()
    {
        await using var otherCompany = new CabinetApi(postgres.ConnectionString);
        Order foreign = otherCompany.Orders.Ready();
        await otherCompany.SaveAsync(foreign);
        Order own = api.Orders.Ready();
        await api.SaveAsync(own);

        string[] exported = await ExportNumbersAsync($"?search={foreign.Number}");

        Assert.Empty(exported);
        Assert.Equal([own.Number], await ExportNumbersAsync());
    }

    [Theory]
    [InlineData("Quebec \"Old\", Town", "\"Quebec \"\"Old\"\", Town, QC\"")]
    [InlineData("=2+3", "\"'=2+3, QC\"")]
    [InlineData("+1", "\"'+1, QC\"")]
    [InlineData("-1", "\"'-1, QC\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1), QC\"")]
    public async Task DestinationIsQuotedAndNeverAFormula(string city, string field)
    {
        Order order = api.Orders.Draft();
        order.SetDestination(city, OrderBuilder.DestinationRegion, OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(order);

        string[] lines = await ExportLinesAsync();

        Assert.Equal($"{order.Number},CrossDock,Draft,{api.Data.Hub.Name},,{field},,0,0,0,0,User 1", lines[1]);
    }

    [Theory]
    [InlineData("type=Pallet", "type")]
    [InlineData("status=Done", "status")]
    [InlineData("status=99", "status")]
    [InlineData("hasOpenAlerts=maybe", "hasOpenAlerts")]
    [InlineData("hubId=", "hubId")]
    [InlineData("datePreset=Yesterday", "datePreset")]
    [InlineData("search=ab", "search")]
    public async Task InvalidParameterIsABadRequestNamingTheField(string query, string field)
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.OrderExport}?{query}");

        await ProblemResponse.AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task ExportIsAnAttachedCsvFile()
    {
        await api.SaveReferenceDataAsync();

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.OrderExport);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(
            "attachment; filename=\"orders.csv\"",
            string.Join(", ", response.Content.Headers.GetValues("Content-Disposition")));
        byte[] body = await response.Content.ReadAsByteArrayAsync(CancellationToken);
        Assert.Equal([.. ByteOrderMark, .. Encoding.UTF8.GetBytes(Header + LineEnd)], body);
    }

    [Fact]
    public async Task ExportRouteIsNotTakenForAnOrderNumber()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        string[] exported = await ExportNumbersAsync();
        OrderDetailDto detail = await api.DetailAsync(order.Number);

        Assert.Equal([order.Number], exported);
        Assert.Equal(order.Number, detail.Number);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // Submitted with a transport of the test's choosing; the builder's Ready orders all go by carrier.
    private Order Submitted(Func<Order, Result> arrangeTransport)
    {
        Order order = api.Orders.Draft();
        api.Orders.AddShipment(order, OrderBuilder.DeclaredStandard);
        arrangeTransport(order).MustSucceed();
        OrderBuilder.Submit(order, CabinetApi.Now);
        return order;
    }

    private string RowOf(Order submitted, string carrier)
    {
        return $"{submitted.Number},CrossDock,Ready,{api.Data.Hub.Name},2026-04-15T16:00:00Z," +
            $"\"{OrderBuilder.DestinationCity}, {OrderBuilder.DestinationRegion}\",{carrier},1,10,0,0,User 1";
    }

    // Drafts, both types, every status, an open alert, the other hub, today and other days; returns one order
    // that has an open alert, for a search to find.
    private async Task<Order> SaveOrdersOfEveryKindAsync()
    {
        Order alerted = api.Orders.Ready(OrderType.Consolidation, scheduledAt: CabinetApi.Now.AddDays(-1));
        OrderBuilder.Unload(alerted, alerted.Shipments.First(), OrderBuilder.DeclaredStandard, photos: 0);
        await api.SaveAsync(
            api.Orders.Draft(),
            api.Orders.Draft(OrderType.Consolidation),
            api.Orders.Ready(scheduledAt: CabinetApi.Now),
            api.Orders.Ready(OrderType.Consolidation),
            api.Orders.Received(scheduledAt: CabinetApi.Now.AddDays(-3)),
            api.Orders.Completed(OrderType.Consolidation, scheduledAt: CabinetApi.Now.AddDays(-20)),
            api.Orders.Ready(hub: api.Data.OtherHub),
            alerted);
        return alerted;
    }

    // The body without its byte order mark, split into lines; the last line ends with a line end too.
    private async Task<string[]> ExportLinesAsync(string query = "")
    {
        using HttpResponseMessage response = await api.SendGetAsync($"{ApiUrls.OrderExport}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] body = await response.Content.ReadAsByteArrayAsync(CancellationToken);
        Assert.Equal(ByteOrderMark, body[..ByteOrderMark.Length]);
        string text = Encoding.UTF8.GetString(body, ByteOrderMark.Length, body.Length - ByteOrderMark.Length);
        Assert.EndsWith(LineEnd, text, StringComparison.Ordinal);
        return text[..^LineEnd.Length].Split(LineEnd);
    }

    // Numbers hold no comma or quote, so the first field of a row is its number as written.
    private async Task<string[]> ExportNumbersAsync(string query = "")
    {
        string[] lines = await ExportLinesAsync(query);
        Assert.Equal(Header, lines[0]);
        return lines.Skip(1).Select(line => line.Split(',')[0]).ToArray();
    }
}
