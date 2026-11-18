using System.Net;
using Freitty.Application.Orders;
using Freitty.Application.Orders.AttachPhotos;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

[Collection(PhotoStorageCollection.Name)]
public sealed class AttachPhotosTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task UploadAttachesThePhotosToTheOperation()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);
        PhotoUpload[] files = [PhotoUpload.Png(sizeBytes: 3000), PhotoUpload.Jpeg(sizeBytes: 4000)];

        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, files);

        Assert.Equal((order.Version + 1, 2), (attached.Version, attached.PhotoCount));
        Assert.Equal(
            [("image/png", 3000L), ("image/jpeg", 4000L)],
            attached.Photos.Select(photo => (photo.ContentType, photo.SizeBytes)));
        DetailShipmentDto shipment = Assert.Single((await api.DetailAsync(order.Number)).Shipments);
        OperationDto operation = Assert.Single(shipment.Operations);
        Assert.Equal(2, operation.PhotoCount);
    }

    [Fact]
    public async Task UploadThatGivesTheOperationItsPhotoClosesTheMissingPhotoAlert()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);

        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, PhotoUpload.Png());

        Assert.Equal([AlertType.MissingPhoto], attached.ResolvedAlertTypes);
        DetailShipmentDto shipment = Assert.Single((await api.DetailAsync(order.Number)).Shipments);
        Assert.Equal(CabinetApi.Now, Assert.Single(shipment.Alerts).ResolvedAt);
    }

    [Fact]
    public async Task UploadToAnOperationThatHasAPhotoClosesNoAlert()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 1);

        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, PhotoUpload.Png());

        Assert.Equal((2, 0), (attached.PhotoCount, attached.ResolvedAlertTypes.Count));
    }

    [Fact]
    public async Task LocationIsThePhotosOfTheOrder()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number.ToLowerInvariant(), unloading.Id, order.Version, PhotoUpload.Png());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ApiUrls.Photos(order.Number.ToLowerInvariant()), response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UploadedPhotoIsServedWithItsBytesTypeAndCacheHeader()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);
        PhotoUpload file = PhotoUpload.WebP();
        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, file);

        using HttpResponseMessage response =
            await api.SendGetAsync(ApiUrls.Photo(order.Number, Assert.Single(attached.Photos).Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(file.Content.Length, response.Content.Headers.ContentLength);
        Assert.Equal("private, max-age=86400, immutable", response.Headers.NonValidated["Cache-Control"].ToString());
        Assert.Equal("same-origin", response.Headers.NonValidated["Cross-Origin-Resource-Policy"].ToString());
        Assert.Equal(file.Content, await response.Content.ReadAsByteArrayAsync(CancellationToken));
    }

    // The browser names the type from the file name; the bytes are a PNG, so that is what is stored and served.
    [Fact]
    public async Task StoredTypeIsTheOneFoundInTheBytes()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);
        PhotoUpload pngNamedJpeg = PhotoUpload.Png("dock.jpg") with { ContentType = "image/jpeg" };

        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, pngNamedJpeg);

        Assert.Equal("image/png", Assert.Single(attached.Photos).ContentType);
    }

    [Fact]
    public async Task FiveFilesAtTheLimitOfTheirSizeAreAccepted()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);
        PhotoUpload[] files = Enumerable.Range(1, AttachPhotosCommandValidator.MaxFileCount)
            .Select(index => PhotoUpload.Jpeg($"dock-{index}.jpg", (int)PhotoFileValidator.MaxSizeBytes))
            .ToArray();

        AttachedPhotosDto attached = await UploadCreatedAsync(order, unloading, files);

        Assert.Equal(AttachPhotosCommandValidator.MaxFileCount, attached.PhotoCount);
    }

    [Fact]
    public async Task UploadToAnOrderOfAnotherCompanyIsNotFound()
    {
        await using var otherCompany = new CabinetApi(postgres.ConnectionString);
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: 0);

        using HttpResponseMessage response =
            await otherCompany.UploadAsync(order.Number, unloading.Id, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "order.not_found",
            $"Order {order.Number} not found.");
    }

    [Fact]
    public async Task UploadToAnUnknownOperationIsNotFoundAndStoresNothing()
    {
        (Order order, _) = await SaveUnloadedAsync(photos: 0);
        Guid unknownOperationId = Guid.CreateVersion7();
        string[] storedBefore = await api.StoredUploadsAsync();

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unknownOperationId, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "operation.not_found",
            $"Order {order.Number} has no operation {unknownOperationId}.");
        Assert.Equal(storedBefore, await api.StoredUploadsAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private async Task<(Order Order, Operation Unloading)> SaveUnloadedAsync(int photos)
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: photos);
        await api.SaveAsync(order);
        return (order, order.Shipments.Single().Operations.Single());
    }

    private async Task<AttachedPhotosDto> UploadCreatedAsync(
        Order order,
        Operation operation,
        params PhotoUpload[] files)
    {
        using HttpResponseMessage response = await api.UploadAsync(order.Number, operation.Id, order.Version, files);
        return await ApiJson.ReadAsync<AttachedPhotosDto>(response, HttpStatusCode.Created);
    }
}
