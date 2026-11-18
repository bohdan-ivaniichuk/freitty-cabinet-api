using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using Freitty.Api.IntegrationTests.Orders;
using Freitty.Api.IntegrationTests.Persistence;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.ListOrders;
using Freitty.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;

namespace Freitty.Api.IntegrationTests;

// The API started for one test: the test's own company and user act as the current user, and the clock stands
// still — at Now, Wednesday 15 April 2026, 12:00 in Toronto, unless the test names another instant. The reporting
// zone is the configured America/Toronto unless the test names another.
internal sealed class CabinetApi : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 4, 15, 16, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase database;
    private readonly HttpClient client;
    private bool referenceDataSaved;

    public CabinetApi(string connectionString, DateTimeOffset? now = null, string? reportingTimeZone = null)
    {
        Data = TestData.Create();
        Orders = new OrderBuilder(Data);
        var factory = new ApiFactory(
            connectionString,
            clock: new FakeTimeProvider(now ?? Now),
            currentUser: new FixedCurrentUser(new CurrentUser(Data.User.Id, Data.Company.Id)),
            reportingTimeZone: reportingTimeZone,
            saveInterceptor: CompetingSave);
        database = new TestDatabase(factory);
        client = factory.CreateClient();
    }

    public TestData Data { get; }

    public OrderBuilder Orders { get; }

    // Unarmed until a test arms it: then the next save of the API runs the test's competing write first.
    public CompetingSaveInterceptor CompetingSave { get; } = new();

    // The first save brings the test's reference data along.
    public Task SaveAsync(params object[] entities)
    {
        object[] referenceData = referenceDataSaved ? [] : Data.Entities();
        referenceDataSaved = true;
        return database.SaveAsync([.. referenceData, .. entities]);
    }

    public async Task<T> GetAsync<T>(string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url, CancellationToken);
        string body = await response.Content.ReadAsStringAsync(CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url} answered {response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, CancellationToken))!;
    }

    public Task<OrderPageDto> ListAsync(string query = "")
    {
        return GetAsync<OrderPageDto>($"/api/v1/orders{query}");
    }

    public Task<OrderDetailDto> DetailAsync(string number)
    {
        return GetAsync<OrderDetailDto>($"/api/v1/orders/{number}");
    }

    public async Task<string[]> ListNumbersAsync(string query = "")
    {
        OrderPageDto page = await ListAsync(query);
        return page.Items.Select(card => card.Number).ToArray();
    }

    public Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        return database.InScopeAsync(action);
    }

    public Task InContextAsync(Func<AppDbContext, Task> action)
    {
        return database.InContextAsync(action);
    }

    public Task<HttpResponseMessage> SendGetAsync(string url)
    {
        return client.GetAsync(url, CancellationToken);
    }

    public Task<HttpResponseMessage> SendPostAsync(string url, object body)
    {
        return client.PostAsJsonAsync(url, body, ApiJson.Options, CancellationToken);
    }

    // A body as it is written, also one that is not what the endpoint expects.
    public async Task<HttpResponseMessage> SendPostAsync(string url, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        return await client.PostAsync(url, content, CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await database.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}
