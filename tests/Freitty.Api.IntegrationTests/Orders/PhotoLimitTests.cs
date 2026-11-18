using System.Net;
using Freitty.Api.Endpoints.Orders.AttachPhotos;
using Freitty.Api.Photos;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// The bounds of the routes that move bytes. Each test starts an API of its own, so the limiters are its own too.
public sealed class PhotoLimitTests(PostgresFixture postgres)
{
    // The held uploads have reached the storage, so they hold their permits: the next one is over the bound.
    [Fact]
    public async Task UploadBeyondTheConcurrentBoundIsRefusedUntilOneEnds()
    {
        var storage = new HeldPhotoStorage();
        await using var api = new CabinetApi(postgres.ConnectionString, photoStorage: storage);
        (Order order, Guid operationId) = await SaveUnloadedAsync(api);
        Task<HttpResponseMessage>[] held = Enumerable.Range(0, PhotoLimits.ConcurrentUploads)
            .Select(_ => api.UploadAsync(order.Number, operationId, order.Version, PhotoUpload.Png()))
            .ToArray();
        await storage.WaitUntilHeldAsync(PhotoLimits.ConcurrentUploads);

        using HttpResponseMessage refused =
            await api.UploadAsync(order.Number, operationId, order.Version, PhotoUpload.Png());
        storage.Release();
        HttpResponseMessage[] ended = await Task.WhenAll(held);

        await ProblemResponse.AssertProblemAsync(refused, HttpStatusCode.TooManyRequests);
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict],
            ended.Select(response => response.StatusCode).Order());
        foreach (HttpResponseMessage response in ended)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task PhotoBeyondTheWindowIsRefusedWithRetryAfter()
    {
        await using var api = new CabinetApi(postgres.ConnectionString);
        await api.SaveReferenceDataAsync();
        string photo = ApiUrls.Photo("FR999999", Guid.CreateVersion7());
        for (int request = 0; request < PhotoLimits.PhotoReadsPerWindow; request++)
        {
            using HttpResponseMessage withinWindow = await api.SendGetAsync(photo);
            Assert.Equal(HttpStatusCode.NotFound, withinWindow.StatusCode);
        }

        using HttpResponseMessage response = await api.SendGetAsync(photo);

        await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.TooManyRequests);
        Assert.NotNull(response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task OtherRoutesAreNotLimited()
    {
        await using var api = new CabinetApi(postgres.ConnectionString);
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        var statuses = new List<HttpStatusCode>();
        for (int request = 0; request <= PhotoLimits.PhotoReadsPerWindow; request++)
        {
            using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Photos(order.Number));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public async Task UploadIsRefusedWhenUploadsAreSwitchedOff()
    {
        await using var api = new CabinetApi(postgres.ConnectionString, photoUploadsEnabled: false);
        (Order order, Guid operationId) = await SaveUnloadedAsync(api);

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, operationId, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.ServiceUnavailable,
            AttachPhotosEndpoint.UploadsDisabledCode,
            "Uploading photos is switched off on this demo; the photos already stored can still be viewed.");
        Assert.Equal(order.Version, (await api.DetailAsync(order.Number)).Version);
    }

    private static async Task<(Order Order, Guid OperationId)> SaveUnloadedAsync(CabinetApi api)
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        await api.SaveAsync(order);
        return (order, order.Shipments.Single().Operations.Single().Id);
    }
}
