using System.Net;
using System.Net.Http.Json;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.SubmitOrder;
using Freitty.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Freitty.Api.IntegrationTests.Seed;

// Each test submits the seed's complete draft, so each has a database of its own. The highest seeded official number
// is FR001681.
public sealed class OrderNumberSequenceTests(PostgresFixture postgres)
{
    private const string SubmittableDraft = "DRAFT-004";
    private const string IncompleteDraft = "DRAFT-003";
    private const string NumberAfterTheSeed = "FR001682";

    [Fact]
    public async Task FirstSubmitAfterSeedingTakesTheNumberAfterTheHighestSeededOne()
    {
        await using var factory = new ApiFactory(
            await postgres.CreateDatabaseAsync(),
            seedEnabled: true,
            new FakeTimeProvider(SeededDatabase.Now));
        using HttpClient client = factory.CreateClient();

        SubmittedOrderDto submitted = await SubmitDraftAsync(client);

        Assert.Equal(NumberAfterTheSeed, submitted.Number);
    }

    [Fact]
    public async Task RefusedSubmitTakesNoNumber()
    {
        await using var factory = new ApiFactory(
            await postgres.CreateDatabaseAsync(),
            seedEnabled: true,
            new FakeTimeProvider(SeededDatabase.Now));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage refused = await PostSubmitAsync(client, IncompleteDraft);
        SubmittedOrderDto submitted = await SubmitDraftAsync(client);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(NumberAfterTheSeed, submitted.Number);
    }

    [Fact]
    public async Task RebuildStartsTheNumbersAfterTheSeedAgain()
    {
        await using var factory = new ApiFactory(
            await postgres.CreateDatabaseAsync(),
            seedEnabled: true,
            new FakeTimeProvider(SeededDatabase.Now),
            seedRefreshOn: true);
        using HttpClient client = factory.CreateClient();
        await SubmitDraftAsync(client);

        await factory.Services.GetRequiredService<SampleData>().RebuildAsync(CancellationToken);
        SubmittedOrderDto submittedAgain = await SubmitDraftAsync(client);

        Assert.Equal(NumberAfterTheSeed, submittedAgain.Number);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static async Task<SubmittedOrderDto> SubmitDraftAsync(HttpClient client)
    {
        using HttpResponseMessage response = await PostSubmitAsync(client, SubmittableDraft);
        return await ApiJson.ReadAsync<SubmittedOrderDto>(response, HttpStatusCode.OK);
    }

    // With the version the draft has now, so only what the draft lacks can refuse it.
    private static async Task<HttpResponseMessage> PostSubmitAsync(HttpClient client, string draftNumber)
    {
        using HttpResponseMessage detailResponse = await client.GetAsync(ApiUrls.Order(draftNumber), CancellationToken);
        OrderDetailDto draft = await ApiJson.ReadAsync<OrderDetailDto>(detailResponse, HttpStatusCode.OK);

        return await client.PostAsJsonAsync(
            ApiUrls.Submit(draftNumber),
            new SubmitRequest(draft.Version),
            ApiJson.Options,
            CancellationToken);
    }
}
