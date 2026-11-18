using System.Net;
using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class OrderSearchTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task SearchMatchesTheWholeOrderNumber()
    {
        (Order match, _) = await SaveTwoAsync();

        string[] numbers = await api.ListNumbersAsync($"?q={match.Number}");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SearchMatchesPartOfTheOrderNumberCaseInsensitively()
    {
        (Order match, _) = await SaveTwoAsync();

        string[] numbers = await api.ListNumbersAsync($"?q={match.Number[..12].ToLowerInvariant()}");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SearchMatchesTheRefNumberOfAnyShipment()
    {
        Order match = api.Orders.Draft(OrderType.Consolidation);
        api.Orders.AddShipment(match, 1, refNumber: "PO-111-AAA");
        api.Orders.AddShipment(match, 1, refNumber: "PO-222-BBB");
        Order other = api.Orders.Draft();
        api.Orders.AddShipment(other, 1, refNumber: "PO-333-CCC");
        await api.SaveAsync(match, other);

        string[] numbers = await api.ListNumbersAsync("?q=po-222-bbb");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SearchMatchesPartOfTheRefNumber()
    {
        Order match = api.Orders.Draft();
        api.Orders.AddShipment(match, 1, refNumber: "PO-444-DDD");
        Order other = api.Orders.Draft();
        api.Orders.AddShipment(other, 1, refNumber: "PO-555-EEE");
        await api.SaveAsync(match, other);

        string[] numbers = await api.ListNumbersAsync("?q=444-d");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SubOrderLabelFindsItsOrder()
    {
        Order match = api.Orders.Ready(OrderType.Consolidation);
        await api.SaveAsync(match, api.Orders.Ready());

        string[] numbers = await api.ListNumbersAsync($"?q={match.Number.ToLowerInvariant()}-2");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SubOrderLabelOfAPositionWithoutShipmentFindsNothing()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        await api.SaveAsync(order);

        string[] numbers = await api.ListNumbersAsync($"?q={order.Number}-3");

        Assert.Empty(numbers);
    }

    [Fact]
    public async Task PositionTooLargeForAnIntIsNoLabel()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        string[] numbers = await api.ListNumbersAsync($"?q={order.Number}-99999999999");

        Assert.Empty(numbers);
    }

    [Theory]
    [InlineData("AB%CD", "ABXCD", "B%C")]
    [InlineData("AB_CD", "ABXCD", "B_C")]
    [InlineData("AB\\CD", "ABXCD", "B\\C")]
    public async Task WildcardInSearchMatchesItself(string matchingRef, string otherRef, string searchText)
    {
        Order match = api.Orders.Draft();
        api.Orders.AddShipment(match, 1, refNumber: matchingRef);
        Order other = api.Orders.Draft();
        api.Orders.AddShipment(other, 1, refNumber: otherRef);
        await api.SaveAsync(match, other);

        string[] numbers = await api.ListNumbersAsync($"?q={Uri.EscapeDataString(searchText)}");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task SearchTextIsTrimmed()
    {
        (Order match, _) = await SaveTwoAsync();

        string[] numbers = await api.ListNumbersAsync($"?q=%20%20{match.Number}%20");

        Assert.Equal(match.Number, Assert.Single(numbers));
    }

    [Fact]
    public async Task WhiteSpaceIsNoSearch()
    {
        await SaveTwoAsync();

        string[] numbers = await api.ListNumbersAsync("?q=%20%20%20%20");

        Assert.Equal(2, numbers.Length);
    }

    [Fact]
    public async Task TwoCharactersAreRefused()
    {
        using HttpResponseMessage response = await api.SendGetAsync("/api/v1/orders?q=FR");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchNeverFindsOrdersOfAnotherCompany()
    {
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).Draft();
        api.Orders.AddShipment(foreign, 1, refNumber: "SHARED-REF-1");
        Order own = api.Orders.Draft();
        await api.SaveAsync([.. otherCompany.Entities(), foreign, own]);

        string[] byRefNumber = await api.ListNumbersAsync("?q=SHARED-REF");
        string[] byNumber = await api.ListNumbersAsync($"?q={foreign.Number}");

        Assert.Empty(byRefNumber);
        Assert.Empty(byNumber);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private async Task<(Order Match, Order Other)> SaveTwoAsync()
    {
        Order match = api.Orders.Ready();
        Order other = api.Orders.Ready();
        await api.SaveAsync(match, other);
        return (match, other);
    }
}
