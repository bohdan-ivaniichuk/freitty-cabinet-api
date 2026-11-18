using System.Net;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Application.Orders.ListPhotos;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Seed;

[Collection(SeededDatabaseCollection.Name)]
public sealed class SeededPhotoTests(SeededDatabase seeded)
{
    private const string LoadedOrder = "FR001383";
    private const int LoadedOrderPhotoCount = 7;

    [Fact]
    public async Task EverySeededOperationHasAsManyPhotosAsItsCount()
    {
        (int PhotoCount, int Photos)[] operations = await seeded.InContextAsync(db => db.Orders
            .SelectMany(order => order.Shipments)
            .SelectMany(shipment => shipment.Operations)
            .Select(operation => new ValueTuple<int, int>(operation.PhotoCount, operation.Photos.Count))
            .ToArrayAsync(CancellationToken));

        Assert.All(operations, operation => Assert.Equal(operation.PhotoCount, operation.Photos));
        Assert.Contains(operations, operation => operation.Photos > 1);
    }

    [Fact]
    public async Task EverySeededPhotoIsASampleImageInStorage()
    {
        string[] keys = await seeded.InContextAsync(db => db.Orders
            .SelectMany(order => order.Shipments)
            .SelectMany(shipment => shipment.Operations)
            .SelectMany(operation => operation.Photos)
            .Select(photo => photo.StorageKey)
            .Distinct()
            .ToArrayAsync(CancellationToken));

        bool[] stored = await seeded.InScopeAsync(async services =>
        {
            IPhotoStorage photoStorage = services.GetRequiredService<IPhotoStorage>();
            Stream?[] contents =
                await Task.WhenAll(keys.Select(key => photoStorage.OpenAsync(key, CancellationToken)));
            bool[] opened = contents.Select(content => content is not null).ToArray();
            foreach (Stream? content in contents)
            {
                content?.Dispose();
            }

            return opened;
        });

        Assert.Equal(4, keys.Length);
        Assert.All(keys, key => Assert.StartsWith(PhotoStorageKeys.SamplesPrefix, key));
        Assert.All(stored, Assert.True);
    }

    [Fact]
    public async Task GalleryOfTheLoadedOrderHoldsTheCountOfItsHeader()
    {
        OrderDetailDto detail = await seeded.GetAsync<OrderDetailDto>(ApiUrls.Order(LoadedOrder));

        IReadOnlyList<PhotoDto> photos = await seeded.GetAsync<IReadOnlyList<PhotoDto>>(ApiUrls.Photos(LoadedOrder));

        Assert.Equal((LoadedOrderPhotoCount, LoadedOrderPhotoCount), (detail.Totals.PhotoCount, photos.Count));
        Assert.Equal(
            [(OperationType.Unloading, 4), (OperationType.Disposal, 2), (OperationType.Restack, 1)],
            photos.CountBy(photo => photo.OperationType).Select(group => (group.Key, group.Value)));
    }

    [Fact]
    public async Task SeededPhotoIsServedAsAPng()
    {
        IReadOnlyList<PhotoDto> photos = await seeded.GetAsync<IReadOnlyList<PhotoDto>>(ApiUrls.Photos(LoadedOrder));

        using HttpResponseMessage response = await seeded.SendGetAsync(ApiUrls.Photo(LoadedOrder, photos[0].Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(photos[0].SizeBytes, response.Content.Headers.ContentLength);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}
