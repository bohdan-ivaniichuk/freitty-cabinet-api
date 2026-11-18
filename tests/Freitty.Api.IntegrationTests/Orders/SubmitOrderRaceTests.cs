using System.Net;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Orders;

// Two submits of one draft: the competing one runs inside the request's save, after the request has read the draft,
// taken its number and decided, and commits first.
public sealed class SubmitOrderRaceTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const string CompetingNumber = "FR-COMPETING";

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task SubmitThatLosesTheRaceIsAConflictAndItsNumberNamesNoOrder()
    {
        Order draft = api.Orders.SubmittableDraft();
        await api.SaveAsync(draft);
        string competingNumber = TestData.Unique(CompetingNumber);
        api.CompetingSave.Arm(() => SubmitElsewhereAsync(draft, competingNumber));

        using HttpResponseMessage response = await api.SubmitAsync(draft.Number, draft.Version);

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            "order.version_conflict",
            $"Order {draft.Number} was changed while this request was being processed. Reload and try again.");
        string[] numbers = await NumbersOfCompanyAsync();
        Assert.Equal(competingNumber, Assert.Single(numbers));
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private Task SubmitElsewhereAsync(Order draft, string number)
    {
        return api.InContextAsync(async db =>
        {
            Order competing = await db.LoadOrderAsync(draft.Id);
            competing.Submit(number, CabinetApi.Now).MustSucceed();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<string[]> NumbersOfCompanyAsync()
    {
        return api.InContextAsync(db => db.Orders
            .Where(order => order.CompanyId == api.Data.Company.Id)
            .Select(order => order.Number)
            .ToArrayAsync(TestContext.Current.CancellationToken));
    }
}
