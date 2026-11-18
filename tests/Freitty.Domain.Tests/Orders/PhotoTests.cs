using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class PhotoTests
{
    [Fact]
    public void AttachedPhotosAreAddedToTheOperationWithTheirCount()
    {
        Order order = TestOrders.Unloaded();
        Operation operation = order.Shipments.Single().Operations.Single();
        IReadOnlyList<NewPhoto> photos = TestOrders.Photos(2);

        PhotoAttachment attachment = order.AttachPhotos(operation.Id, photos, TestOrders.At(1)).MustSucceed();

        Assert.Equal(3, operation.PhotoCount);
        Assert.Equal(3, operation.Photos.Count);
        Assert.Equal(
            photos.Select(photo => (photo.StorageKey, photo.ContentType, photo.SizeBytes)),
            attachment.Photos.Select(photo => (photo.StorageKey, photo.ContentType, photo.SizeBytes)));
        Assert.All(attachment.Photos, photo => Assert.Equal(
            (operation.Id, TestOrders.At(1)),
            (photo.OperationId, photo.UploadedAt)));
        Assert.Same(operation, attachment.Operation);
    }

    [Fact]
    public void AttachingPhotosChangesTheOrder()
    {
        Order order = TestOrders.Unloaded();
        Operation operation = order.Shipments.Single().Operations.Single();
        int versionBefore = order.Version;

        order.AttachPhotos(operation.Id, TestOrders.Photos(1), TestOrders.At(1)).MustSucceed();

        Assert.Equal((versionBefore + 1, TestOrders.At(1)), (order.Version, order.UpdatedAt));
    }

    [Theory]
    [InlineData(OperationType.Unloading)]
    [InlineData(OperationType.Disposal)]
    public void PhotoOfTheOperationThatLacksOneResolvesTheMissingPhotoAlert(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();
        Operation operation = Record(order, shipment, TestOrders.OperationOf(type, 1, photoCount: 0));

        PhotoAttachment attachment =
            order.AttachPhotos(operation.Id, TestOrders.Photos(1), TestOrders.At(2)).MustSucceed();

        Assert.Equal([AlertType.MissingPhoto], attachment.ResolvedAlertTypes);
        Alert alert = Assert.Single(shipment.Alerts, alert => alert.Type == AlertType.MissingPhoto);
        Assert.Equal(TestOrders.At(2), alert.ResolvedAt);
    }

    [Fact]
    public void MissingPhotoAlertStaysOpenWhileAnotherOperationLacksAPhoto()
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();
        Operation unloading =
            Record(order, shipment, TestOrders.OperationOf(OperationType.Unloading, 1, photoCount: 0));
        Record(order, shipment, TestOrders.OperationOf(OperationType.Disposal, 1, photoCount: 0));

        PhotoAttachment attachment =
            order.AttachPhotos(unloading.Id, TestOrders.Photos(1), TestOrders.At(2)).MustSucceed();

        Assert.Empty(attachment.ResolvedAlertTypes);
        Assert.Null(Assert.Single(shipment.Alerts, alert => alert.Type == AlertType.MissingPhoto).ResolvedAt);
    }

    [Fact]
    public void PhotoOfAnOperationThatNeedsNoneLeavesTheMissingPhotoAlertOpen()
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();
        Record(order, shipment, TestOrders.OperationOf(OperationType.Disposal, 1, photoCount: 0));
        Operation restack = Record(order, shipment, TestOrders.OperationOf(OperationType.Restack, 1, photoCount: 0));

        PhotoAttachment attachment =
            order.AttachPhotos(restack.Id, TestOrders.Photos(1), TestOrders.At(2)).MustSucceed();

        Assert.Empty(attachment.ResolvedAlertTypes);
        Assert.Null(Assert.Single(shipment.Alerts, alert => alert.Type == AlertType.MissingPhoto).ResolvedAt);
    }

    [Fact]
    public void AttachingPhotosLeavesAQuantityDeltaAlertAlone()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Operation unloading = Record(
            order,
            shipment,
            TestOrders.OperationOf(OperationType.Unloading, TestOrders.DeclaredStandard + 1, photoCount: 0));

        PhotoAttachment attachment =
            order.AttachPhotos(unloading.Id, TestOrders.Photos(1), TestOrders.At(2)).MustSucceed();

        Assert.Equal([AlertType.MissingPhoto], attachment.ResolvedAlertTypes);
        Alert quantityDelta = Assert.Single(shipment.Alerts, alert => alert.Type == AlertType.QuantityDelta);
        Assert.Null(quantityDelta.ResolvedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Completed)]
    public void AttachingPhotosToAnOrderThatIsNotActiveIsRefused(OrderStatus status)
    {
        Order order = TestOrders.InStatus(status);
        Guid operationId = order.Shipments.SelectMany(shipment => shipment.Operations).FirstOrDefault()?.Id
            ?? Guid.CreateVersion7();
        int versionBefore = order.Version;

        Result<PhotoAttachment> result = order.AttachPhotos(operationId, TestOrders.Photos(1), TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, OrderErrors.NotActiveCode);
        Assert.Equal(versionBefore, order.Version);
    }

    [Fact]
    public void AttachingPhotosToAnUnknownOperationIsRefused()
    {
        Order order = TestOrders.Unloaded();

        Result<PhotoAttachment> result =
            order.AttachPhotos(Guid.CreateVersion7(), TestOrders.Photos(1), TestOrders.At(1));

        AssertRefusal.NotFound(result.Error, OrderErrors.OperationNotFoundCode);
    }

    [Fact]
    public void PhotosBeyondTheLimitOfAnOperationAreRefused()
    {
        Order order = TestOrders.Unloaded();
        Operation operation = order.Shipments.Single().Operations.Single();
        order.AttachPhotos(operation.Id, TestOrders.Photos(Operation.MaxPhotoCount - 2), TestOrders.At(1))
            .MustSucceed();

        Result<PhotoAttachment> result = order.AttachPhotos(operation.Id, TestOrders.Photos(2), TestOrders.At(2));

        DomainError error = AssertRefusal.Conflict(result.Error, OrderErrors.PhotoLimitCode);
        Assert.Equal(
            $"The Unloading of FR001383-1 has 9 photos; 2 more would exceed the limit of {Operation.MaxPhotoCount}.",
            error.Message);
        Assert.Equal(Operation.MaxPhotoCount - 1, operation.PhotoCount);
    }

    [Fact]
    public void PhotosUpToTheLimitOfAnOperationAreAttached()
    {
        Order order = TestOrders.Unloaded();
        Operation operation = order.Shipments.Single().Operations.Single();

        order.AttachPhotos(operation.Id, TestOrders.Photos(Operation.MaxPhotoCount - 1), TestOrders.At(1))
            .MustSucceed();

        Assert.Equal(Operation.MaxPhotoCount, operation.PhotoCount);
    }

    [Fact]
    public void OperationRecordedWithMorePhotosThanTheLimitIsAContractViolation()
    {
        Order order = TestOrders.Ready();
        NewOperation operation = TestOrders.OperationOf(
            OperationType.Unloading,
            1,
            photoCount: Operation.MaxPhotoCount + 1);

        Assert.ThrowsAny<ArgumentException>(() =>
            order.RecordOperation(order.Shipments.Single().Id, operation, TestOrders.At(1)));
    }

    [Fact]
    public void AttachingNoPhotoIsAContractViolation()
    {
        Order order = TestOrders.Unloaded();
        Operation operation = order.Shipments.Single().Operations.Single();

        Assert.ThrowsAny<ArgumentException>(() => order.AttachPhotos(operation.Id, [], TestOrders.At(1)));
    }

    private static Operation Record(Order order, Shipment shipment, NewOperation operation)
    {
        return order.RecordOperation(shipment.Id, operation, TestOrders.At(1)).MustSucceed();
    }
}
