using System.Net;
using Freitty.Application.Orders;
using Freitty.Application.Orders.ListPhotos;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// Rows only: the photos here have no bytes in storage, so a request for one is the 404 of missing bytes.
public sealed class PhotoListTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    // Shipment 2 is worked first and gets a photo later still: the list goes by shipment, then by operation, then by
    // upload time.
    [Fact]
    public async Task PhotosAreListedInTheOrderOfThePage()
    {
        Order order = api.Orders.Ready(OrderType.Consolidation);
        Shipment first = order.Shipments.First();
        Shipment second = order.Shipments.Last();
        OrderBuilder.Unload(order, second, 1, photos: 1, at: At(minutes: 1));
        OrderBuilder.Unload(order, first, 1, photos: 1, at: At(minutes: 2));
        Operation secondUnloading = second.Operations.Single();
        order.AttachPhotos(secondUnloading.Id, OrderBuilder.Photos(1), At(minutes: 3)).MustSucceed();
        await api.SaveAsync(order);

        IReadOnlyList<PhotoDto> photos = await api.GetAsync<IReadOnlyList<PhotoDto>>(ApiUrls.Photos(order.Number));

        Assert.Equal(
            [
                (first.Operations.Single().Id, $"{order.Number}-1", At(minutes: 2)),
                (secondUnloading.Id, $"{order.Number}-2", At(minutes: 1)),
                (secondUnloading.Id, $"{order.Number}-2", At(minutes: 3)),
            ],
            photos.Select(photo => (photo.OperationId, photo.ShipmentLabel, photo.UploadedAt)));
        Assert.All(photos, photo => Assert.Equal(
            (OperationType.Unloading, OrderBuilder.PhotoContentType, OrderBuilder.PhotoSizeBytes),
            (photo.OperationType, photo.ContentType, photo.SizeBytes)));
    }

    [Fact]
    public async Task OrderWithoutPhotosHasAnEmptyList()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        IReadOnlyList<PhotoDto> photos = await api.GetAsync<IReadOnlyList<PhotoDto>>(ApiUrls.Photos(order.Number));

        Assert.Empty(photos);
    }

    [Fact]
    public async Task PhotosOfAnUnknownOrderAreNotFound()
    {
        await api.SaveReferenceDataAsync();

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Photos("FR999999"));

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            OrderRequestErrors.OrderNotFoundCode,
            "Order FR999999 not found.");
    }

    [Fact]
    public async Task PhotoOfAnotherOrderUnderThisNumberIsNotFound()
    {
        Order order = api.Orders.Ready();
        Order other = api.Orders.Ready();
        OrderBuilder.Unload(other, other.Shipments.Single(), 1, photos: 1);
        await api.SaveAsync(order, other);
        Guid photoId = other.Shipments.Single().Operations.Single().Photos.Single().Id;

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Photo(order.Number, photoId));

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            PhotoRequestErrors.PhotoNotFoundCode,
            $"Order {order.Number} has no photo {photoId}.");
    }

    [Fact]
    public async Task PhotoWhoseBytesAreNotInStorageIsNotFound()
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), 1, photos: 1);
        await api.SaveAsync(order);
        Guid photoId = order.Shipments.Single().Operations.Single().Photos.Single().Id;

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Photo(order.Number, photoId));

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.NotFound,
            PhotoRequestErrors.PhotoNotFoundCode,
            $"Order {order.Number} has no photo {photoId}.");
    }

    [Fact]
    public async Task PhotoIdThatIsNotAUuidIsABadRequest()
    {
        Order order = api.Orders.Ready();
        await api.SaveAsync(order);

        using HttpResponseMessage response = await api.SendGetAsync(ApiUrls.Photo(order.Number, "not-a-uuid"));

        await ProblemResponse.AssertValidationProblemAsync(response, "photoId");
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static DateTimeOffset At(int minutes)
    {
        return OrderBuilder.BuiltAt.AddMinutes(minutes);
    }
}
