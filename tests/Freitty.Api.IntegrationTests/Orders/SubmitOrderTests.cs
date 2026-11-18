using System.Globalization;
using System.Net;
using System.Text.Json;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.SubmitOrder;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Orders;

public sealed class SubmitOrderTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string OfficialNumberPattern = @"^FR\d{6,}$";

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task SubmitMovesTheDraftToReadyUnderAnOfficialNumber()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        SubmittedOrderDto submitted = await SubmitOkAsync(draft.Number, draft.Version);

        Assert.Matches(OfficialNumberPattern, submitted.Number);
        Assert.Equal((OrderStatus.Ready, draft.Version + 1), (submitted.Status, submitted.Version));
        OrderDetailDto detail = await api.DetailAsync(submitted.Number);
        Assert.Equal((OrderStatus.Ready, submitted.Version), (detail.Status, detail.Version));
        using HttpResponseMessage underDraftNumber = await api.SendGetAsync(ApiUrls.Order(draft.Number));
        Assert.Equal(HttpStatusCode.NotFound, underDraftNumber.StatusCode);
    }

    [Fact]
    public async Task SubmitIsStampedWithTheClock()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        SubmittedOrderDto submitted = await SubmitOkAsync(draft.Number, draft.Version);

        DateTimeOffset? submittedAt = await api.InContextAsync(db => db.Orders
            .Where(order => order.Number == submitted.Number)
            .Select(order => order.SubmittedAt)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CabinetApi.Now, submittedAt);
    }

    [Fact]
    public async Task DraftNumberIsMatchedCaseInsensitively()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        SubmittedOrderDto submitted = await SubmitOkAsync(draft.Number.ToLowerInvariant(), draft.Version);

        Assert.Equal(OrderStatus.Ready, submitted.Status);
    }

    [Fact]
    public async Task LaterSubmitGetsAHigherNumber()
    {
        Order first = api.Orders.SubmittableDraft();
        Order second = api.Orders.SubmittableDraft();
        await api.SaveAsync(first, second);

        SubmittedOrderDto firstSubmitted = await SubmitOkAsync(first.Number, first.Version);
        SubmittedOrderDto secondSubmitted = await SubmitOkAsync(second.Number, second.Version);

        Assert.True(
            NumericPart(secondSubmitted.Number) > NumericPart(firstSubmitted.Number),
            $"{secondSubmitted.Number} does not follow {firstSubmitted.Number}.");
    }

    [Fact]
    public async Task DraftWithoutWhatSubmitNeedsIsAConflictListingIt()
    {
        Order draft = api.Orders.Draft(OrderType.Consolidation);
        draft.Schedule(CabinetApi.Now.AddDays(3), OrderBuilder.BuiltAt).MustSucceed();
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await api.SubmitAsync(draft.Number, draft.Version);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.not_ready_to_submit",
            $"Order {draft.Number} cannot be submitted; it still needs a destination, a transport arrangement, " +
            "at least 2 shipments for a Consolidation.");
        await AssertUnchangedAsync(draft);
    }

    [Fact]
    public async Task SubmittedOrderIsAConflictAsNotADraft()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.SubmitAsync(order.Number, order.Version);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.not_draft",
            $"Order {order.Number} is Ready; only a draft can be changed this way.");
        await AssertUnchangedAsync(order);
    }

    [Fact]
    public async Task StaleVersionIsAConflictNamingBothVersions()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await api.SubmitAsync(draft.Number, draft.Version - 1);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.version_conflict",
            $"Order {draft.Number} was changed since you loaded it (version {draft.Version}, expected " +
            $"{draft.Version - 1}). Reload and try again.");
        await AssertUnchangedAsync(draft);
    }

    [Fact]
    public async Task UnknownOrderIsNotFound()
    {
        await api.SaveReferenceDataAsync();

        using HttpResponseMessage response = await api.SubmitAsync("DRAFT-999", 1);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            "Order DRAFT-999 not found.");
    }

    [Fact]
    public async Task DraftOfAnotherCompanyIsNotFound()
    {
        TestData otherCompany = TestData.Create();
        Order foreign = new OrderBuilder(otherCompany).SubmittableDraft();
        await api.SaveAsync([.. otherCompany.Entities(), foreign]);

        using HttpResponseMessage response = await api.SubmitAsync(foreign.Number, foreign.Version);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            $"Order {foreign.Number} not found.");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "expectedVersion": 0 }""")]
    [InlineData("""{ "expectedVersion": -1 }""")]
    [InlineData("""{ "expectedVersion": "three" }""")]
    [InlineData("""{ "expectedVersion": 1.5 }""")]
    public async Task MissingOrInvalidVersionIsABadRequestNamingIt(string json)
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await api.SendRawPostAsync(ApiUrls.Submit(draft.Number), json);

        await ProblemResponse.AssertValidationProblemAsync(response, "expectedVersion");
        await AssertUnchangedAsync(draft);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"expectedVersion\":")]
    public async Task BodyThatIsNotAJsonObjectIsABadRequestOfTheWholeBody(string json)
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);

        using HttpResponseMessage response = await api.SendRawPostAsync(ApiUrls.Submit(draft.Number), json);

        JsonElement problem = await ProblemResponse.AssertValidationProblemAsync(response, "serializerErrors");
        Assert.Equal(
            "The request body is not valid JSON for this endpoint.",
            problem.GetProperty("detail").GetString());
        await AssertUnchangedAsync(draft);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static long NumericPart(string officialNumber)
    {
        return long.Parse(officialNumber[2..], CultureInfo.InvariantCulture);
    }

    private async Task<SubmittedOrderDto> SubmitOkAsync(string orderNumber, int expectedVersion)
    {
        using HttpResponseMessage response = await api.SubmitAsync(orderNumber, expectedVersion);
        return await ApiJson.ReadAsync<SubmittedOrderDto>(response, HttpStatusCode.OK);
    }

    private async Task AssertUnchangedAsync(Order order)
    {
        OrderDetailDto detail = await api.DetailAsync(order.Number);
        Assert.Equal((order.Status, order.Version), (detail.Status, detail.Version));
    }
}
