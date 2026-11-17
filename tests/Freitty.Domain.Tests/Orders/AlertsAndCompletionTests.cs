using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class AlertsAndCompletionTests
{
    [Theory]
    [InlineData(OperationType.Unloading)]
    [InlineData(OperationType.Disposal)]
    public void OperationWithoutAPhotoRaisesAMissingPhotoAlert(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();

        Record(order, shipment, TestOrders.OperationOf(type, 1, photoCount: 0), TestOrders.At(1));

        Alert alert = Assert.Single(shipment.Alerts, alert => alert.Type == AlertType.MissingPhoto);
        Assert.Equal(TestOrders.At(1), alert.RaisedAt);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void MissingPhotoAlertIsRaisedOncePerShipment()
    {
        Order order = TestOrders.Ready(OrderType.Consolidation);
        Shipment first = order.Shipments.First();
        Shipment second = order.Shipments.Last();
        NewOperation unloadingWithoutPhoto = TestOrders.OperationOf(OperationType.Unloading, 3, photoCount: 0);

        Record(order, first, unloadingWithoutPhoto, TestOrders.At(1));
        Record(order, first, unloadingWithoutPhoto, TestOrders.At(2));
        Record(order, second, unloadingWithoutPhoto, TestOrders.At(3));

        Assert.Single(first.Alerts, alert => alert.Type == AlertType.MissingPhoto);
        Assert.Single(second.Alerts, alert => alert.Type == AlertType.MissingPhoto);
    }

    [Theory]
    [InlineData(OperationType.Unloading)]
    [InlineData(OperationType.Disposal)]
    public void OperationWithAPhotoRaisesNoMissingPhotoAlert(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();

        Record(order, shipment, TestOrders.OperationOf(type, 1, photoCount: 1), TestOrders.At(1));

        Assert.DoesNotContain(shipment.Alerts, alert => alert.Type == AlertType.MissingPhoto);
    }

    [Theory]
    [InlineData(OperationType.Restack)]
    [InlineData(OperationType.Loading)]
    public void OperationThatNeedsNoPhotoRaisesNoMissingPhotoAlertWithoutOne(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();

        Record(order, shipment, TestOrders.OperationOf(type, 1, photoCount: 0), TestOrders.At(1));

        Assert.Empty(shipment.Alerts);
    }

    [Fact]
    public void ResolveAlertSetsResolvedAt()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Record(order, shipment, TestOrders.OperationOf(OperationType.Unloading, 1, photoCount: 0), TestOrders.At(1));
        Alert alert = shipment.Alerts.Single();

        Result result = order.ResolveAlert(alert.Id, TestOrders.At(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(TestOrders.At(2), alert.ResolvedAt);
        Assert.False(order.HasOpenAlerts);
    }

    [Fact]
    public void ResolvingAnUnknownAlertIsRefused()
    {
        Order order = TestOrders.Ready();

        Result result = order.ResolveAlert(Guid.CreateVersion7(), TestOrders.At(1));

        AssertRefusal.NotFound(result.Error, "alert.not_found");
    }

    [Fact]
    public void ResolvingAResolvedAlertIsRefused()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Record(order, shipment, TestOrders.OperationOf(OperationType.Unloading, 1, photoCount: 0), TestOrders.At(1));
        Guid alertId = shipment.Alerts.Single().Id;
        order.ResolveAlert(alertId, TestOrders.At(2)).MustSucceed();

        Result result = order.ResolveAlert(alertId, TestOrders.At(3));

        AssertRefusal.Conflict(result.Error, "alert.already_resolved");
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Completed)]
    public void ResolvingAnAlertIsRefusedOnAnOrderThatIsNotActive(OrderStatus status)
    {
        Order order = TestOrders.InStatus(status);

        Result result = order.ResolveAlert(Guid.CreateVersion7(), TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.not_active");
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Completed)]
    public void CompleteIsRefusedWhenTheOrderIsNotInProgress(OrderStatus status)
    {
        Order order = TestOrders.InStatus(status);

        Result result = order.Complete(TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.not_in_progress");
    }

    [Fact]
    public void CompleteIsRefusedWhileAShipmentIsNotReceived()
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();
        NewOperation loading = TestOrders.OperationOf(OperationType.Loading, TestOrders.DeclaredStandard);
        Record(order, shipment, loading, TestOrders.At(1));

        Result result = order.Complete(TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "order.receiving_not_finished");
    }

    [Fact]
    public void CompleteIsRefusedWhileSomethingIsOnStock()
    {
        Order order = TestOrders.Unloaded();
        order.FinishReceiving(order.Shipments.Single().Id, TestOrders.At(1)).MustSucceed();

        Result result = order.Complete(TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "order.stock_remaining");
    }

    [Fact]
    public void CompleteIsRefusedWhileAnAlertIsOpen()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        NewOperation unloadingWithoutPhoto =
            TestOrders.OperationOf(OperationType.Unloading, TestOrders.DeclaredStandard, photoCount: 0);
        Record(order, shipment, unloadingWithoutPhoto, TestOrders.At(1));
        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();
        NewOperation loading = TestOrders.OperationOf(OperationType.Loading, TestOrders.DeclaredStandard);
        Record(order, shipment, loading, TestOrders.At(3));

        Result result = order.Complete(TestOrders.At(4));

        AssertRefusal.Conflict(result.Error, "order.open_alerts");
    }

    [Fact]
    public void CompleteReportsUnfinishedReceivingBeforeStockAndOpenAlerts()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Record(order, shipment, TestOrders.OperationOf(OperationType.Unloading, 4, photoCount: 0), TestOrders.At(1));

        Result result = order.Complete(TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "order.receiving_not_finished");
    }

    [Fact]
    public void CompleteReportsStockBeforeOpenAlerts()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Record(order, shipment, TestOrders.OperationOf(OperationType.Unloading, 4, photoCount: 0), TestOrders.At(1));
        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();

        Result result = order.Complete(TestOrders.At(3));

        AssertRefusal.Conflict(result.Error, "order.stock_remaining");
    }

    [Fact]
    public void CompleteSetsCompletedAndCompletedAt()
    {
        Order order = TestOrders.ReadyToComplete();

        Result result = order.Complete(TestOrders.At(4));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(TestOrders.At(4), order.CompletedAt);
    }

    [Fact]
    public void OrderWhoseOnlyShipmentNeverArrivedCanBeClosed()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        order.FinishReceiving(shipment.Id, TestOrders.At(1)).MustSucceed();
        order.ResolveAlert(shipment.Alerts.Single().Id, TestOrders.At(2)).MustSucceed();

        Result result = order.Complete(TestOrders.At(3));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    private static void Record(Order order, Shipment shipment, NewOperation operation, DateTimeOffset now)
    {
        order.RecordOperation(shipment.Id, operation, now).MustSucceed();
    }
}
