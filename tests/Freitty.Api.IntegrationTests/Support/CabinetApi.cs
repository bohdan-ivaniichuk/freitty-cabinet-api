using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.GetPipeline;
using Freitty.Application.Orders.ListOrders;
using Freitty.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;

namespace Freitty.Api.IntegrationTests.Support;

// The API started for one test: the test's own company and user act as the current user, and the clock stands
// still at Now unless the test names another instant. The reporting zone is the configured America/Toronto unless
// the test names another. The API starts on first use, so a test that starts one of its own starts no other.
internal sealed class CabinetApi : IAsyncDisposable
{
    // Wednesday 15 April 2026, 12:00 in Toronto, the reporting zone: daylight saving time (UTC−4) has been in effect
    // since 8 March.
    public static readonly DateTimeOffset Now = Toronto.At(2026, 4, 15, 12);

    private readonly ApiFactory factory;
    private readonly TestDatabase database;
    private HttpClient? client;
    private bool referenceDataSaved;

    public CabinetApi(
        string connectionString,
        DateTimeOffset? now = null,
        string? reportingTimeZone = null,
        IPhotoStorage? photoStorage = null,
        bool photoUploadsEnabled = true)
    {
        Data = TestData.Create();
        Orders = new OrderBuilder(Data);
        factory = new ApiFactory(
            connectionString,
            clock: new FakeTimeProvider(now ?? Now),
            currentUser: new FixedCurrentUser(new CurrentUser(Data.User.Id, Data.Company.Id)),
            reportingTimeZone: reportingTimeZone,
            saveInterceptor: CompetingSave,
            photoStorage: photoStorage,
            photoUploadsEnabled: photoUploadsEnabled);
        database = new TestDatabase(factory);
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

    // For a test whose user, company and hubs are all it needs.
    public Task SaveReferenceDataAsync()
    {
        return SaveAsync();
    }

    public async Task<T> GetAsync<T>(string url)
    {
        using HttpResponseMessage response = await SendGetAsync(url);
        return await ApiJson.ReadAsync<T>(response, HttpStatusCode.OK);
    }

    public async Task<T> PostAsync<T>(string url, object body, HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await SendPostAsync(url, body);
        return await ApiJson.ReadAsync<T>(response, expectedStatus);
    }

    public Task<OrderPageDto> ListAsync(string query = "")
    {
        return GetAsync<OrderPageDto>($"{ApiUrls.Orders}{query}");
    }

    public Task<OrderDetailDto> DetailAsync(string number)
    {
        return GetAsync<OrderDetailDto>(ApiUrls.Order(number));
    }

    public async Task<string[]> ListNumbersAsync(string query = "")
    {
        OrderPageDto page = await ListAsync(query);
        return page.Items.Select(card => card.Number).ToArray();
    }

    public Task<HttpResponseMessage> RecordAsync(string orderNumber, OperationRequest request)
    {
        return SendPostAsync(ApiUrls.Operations(orderNumber), request);
    }

    public Task<PipelineDto> PipelineAsync(string query = "")
    {
        return GetAsync<PipelineDto>($"{ApiUrls.OrderPipeline}{query}");
    }

    public Task<HttpResponseMessage> SubmitAsync(string orderNumber, int expectedVersion)
    {
        return SendPostAsync(ApiUrls.Submit(orderNumber), new SubmitRequest(expectedVersion));
    }

    public async Task<HttpResponseMessage> UploadAsync(
        string orderNumber,
        Guid operationId,
        int? version,
        params PhotoUpload[] files)
    {
        using MultipartFormDataContent form = PhotoUpload.Form(version, files);
        return await SendFormAsync(ApiUrls.OperationPhotos(orderNumber, operationId), form);
    }

    public Task<HttpResponseMessage> SendFormAsync(string url, MultipartFormDataContent form)
    {
        return Client.PostAsync(url, form, CancellationToken);
    }

    public Task<string[]> StoredUploadsAsync()
    {
        return StoredUploads.ListAsync(factory.Services);
    }

    public Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        return database.InScopeAsync(action);
    }

    public Task InContextAsync(Func<AppDbContext, Task> action)
    {
        return database.InContextAsync(action);
    }

    public Task<T> InContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        return database.InContextAsync(action);
    }

    public Task<HttpResponseMessage> SendGetAsync(string url)
    {
        return Client.GetAsync(url, CancellationToken);
    }

    public Task<HttpResponseMessage> SendPostAsync(string url, object body)
    {
        return Client.PostAsJsonAsync(url, body, ApiJson.Options, CancellationToken);
    }

    // A body as it is written, also one that is not what the endpoint expects.
    public async Task<HttpResponseMessage> SendRawPostAsync(
        string url,
        string body,
        string mediaType = MediaTypeNames.Application.Json)
    {
        using var content = new StringContent(body, Encoding.UTF8, mediaType);
        return await Client.PostAsync(url, content, CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        await database.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private HttpClient Client => client ??= factory.CreateClient();
}
