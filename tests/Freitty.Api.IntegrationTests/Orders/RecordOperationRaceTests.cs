using System.Net;
using System.Text.Json;
using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// Two writers on one order: the competing write runs inside the request's save, after the request has read the
// order and decided, and commits first.
public sealed class RecordOperationRaceTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task WriterThatLosesOnTheOrderRowIsAConflict()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        api.CompetingSave.Arm(() => UnloadElsewhereAsync(order, quantity: 3, photos: 1));

        using HttpResponseMessage response = await api.SendPostAsync(
            OperationRequest.UrlOf(order.Number),
            OperationRequest.Unloading(order, 4));

        await AssertLostAsync(response, order.Number);
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Equal(order.Version + 1, detail.Version);
        Assert.Equal(3, Assert.Single(Assert.Single(detail.Shipments).Operations).Quantity);
    }

    [Fact]
    public async Task WriterThatLosesOnTheOpenAlertIndexIsAConflict()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        api.CompetingSave.Arm(() => UnloadElsewhereAsync(order, quantity: 3, photos: 0));

        using HttpResponseMessage response = await api.SendPostAsync(
            OperationRequest.UrlOf(order.Number),
            OperationRequest.Unloading(order, 4, photos: 0));

        await AssertLostAsync(response, order.Number);
        DetailShipmentDto shipment = Assert.Single((await api.DetailAsync(order.Number)).Shipments);
        Assert.Equal(3, Assert.Single(shipment.Operations).Quantity);
        AlertDto alert = Assert.Single(shipment.Alerts);
        Assert.Equal((AlertType.MissingPhoto, (DateTimeOffset?)null), (alert.Type, alert.ResolvedAt));
    }

    [Fact]
    public async Task OfTwoSimultaneousRequestsOneIsRecorded()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);
        OperationRequest unloading = OperationRequest.Unloading(order, 4);

        HttpResponseMessage[] responses = await Task.WhenAll(
            api.SendPostAsync(OperationRequest.UrlOf(order.Number), unloading),
            api.SendPostAsync(OperationRequest.UrlOf(order.Number), unloading));
        HttpStatusCode[] statuses = DisposeAll(responses);

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses.Order());
        Assert.Single(Assert.Single((await api.DetailAsync(order.Number)).Shipments).Operations);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static HttpStatusCode[] DisposeAll(HttpResponseMessage[] responses)
    {
        HttpStatusCode[] statuses = responses.Select(response => response.StatusCode).ToArray();
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        return statuses;
    }

    private static async Task AssertLostAsync(HttpResponseMessage response, string number)
    {
        JsonElement problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order.version_conflict", problem.GetProperty("code").GetString());
        Assert.Equal(
            $"Order {number} was changed while this request was being processed. Reload and try again.",
            problem.GetProperty("detail").GetString());
    }

    private Task UnloadElsewhereAsync(Order order, int quantity, int photos)
    {
        return api.InContextAsync(async db =>
        {
            Order competing = await db.LoadOrderAsync(order.Id);
            OrderBuilder.Unload(competing, competing.Shipments.Single(), quantity, photos: photos, at: CabinetApi.Now);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
