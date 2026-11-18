using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class HubWorkTests
{
    [Theory]
    [InlineData(HubWork.Arrival)]
    [InlineData(HubWork.DockAssignment)]
    [InlineData(HubWork.Operation)]
    [InlineData(HubWork.FinishReceiving)]
    [InlineData(HubWork.WarehouseNote)]
    public void HubWorkStartsAReadyOrder(HubWork work)
    {
        Order order = TestOrders.Ready();

        DomainError? error = Perform(order, work, TestOrders.At(5));

        Assert.Null(error);
        Assert.Equal(OrderStatus.InProgress, order.Status);
        Assert.Equal(TestOrders.At(5), order.StartedAt);
    }

    [Fact]
    public void LaterHubWorkKeepsTheInstantTheOrderStarted()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;
        order.SetWarehouseNote(shipmentId, "Fragile", TestOrders.At(1)).MustSucceed();

        order.AssignDock(shipmentId, "D4", "B2", "Alex Kim", TestOrders.At(2)).MustSucceed();

        Assert.Equal(TestOrders.At(1), order.StartedAt);
    }

    [Theory]
    [InlineData(HubWork.Arrival)]
    [InlineData(HubWork.DockAssignment)]
    [InlineData(HubWork.Operation)]
    [InlineData(HubWork.FinishReceiving)]
    [InlineData(HubWork.WarehouseNote)]
    public void HubWorkIsRefusedOnADraft(HubWork work)
    {
        Order order = TestOrders.SubmittableDraft();

        DomainError? error = Perform(order, work, TestOrders.At(1));

        AssertRefusal.Conflict(error, "order.not_active");
    }

    [Theory]
    [InlineData(HubWork.Arrival)]
    [InlineData(HubWork.DockAssignment)]
    [InlineData(HubWork.Operation)]
    [InlineData(HubWork.FinishReceiving)]
    [InlineData(HubWork.WarehouseNote)]
    public void HubWorkIsRefusedOnACompletedOrder(HubWork work)
    {
        Order order = TestOrders.Completed();

        DomainError? error = Perform(order, work, TestOrders.At(1));

        AssertRefusal.Conflict(error, "order.not_active");
    }

    [Fact]
    public void SecondArrivalOfOneShipmentIsRefused()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;
        order.RecordArrival(shipmentId, TrailerType.DryVan53, "TRK-1", "TR-1", TestOrders.At(1)).MustSucceed();

        Result result = order.RecordArrival(shipmentId, TrailerType.Reefer53, "TRK-2", "TR-2", TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "shipment.already_arrived");
    }

    [Fact]
    public void SecondDockAssignmentReplacesTheFirst()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        order.AssignDock(shipment.Id, "D1", "B1", "Alex Kim", TestOrders.At(1)).MustSucceed();

        order.AssignDock(shipment.Id, "D4", "B2", "Jo Park", TestOrders.At(2)).MustSucceed();

        Assert.Equal("D4", shipment.Dock);
        Assert.Equal("B2", shipment.Bay);
        Assert.Equal("Jo Park", shipment.FloorLead);
        Assert.Equal(TestOrders.At(2), shipment.DockAssignedAt);
    }

    [Theory]
    [InlineData(OperationType.Unloading, null)]
    [InlineData(OperationType.Loading, null)]
    [InlineData(OperationType.Disposal, "TR-1")]
    [InlineData(OperationType.Restack, "TR-1")]
    public void TrailerNumberThatDoesNotFitTheOperationTypeIsAContractViolation(
        OperationType type,
        string? trailerNumber)
    {
        Order order = TestOrders.Unloaded();
        Guid shipmentId = order.Shipments.Single().Id;
        var operation = new NewOperation(
            type,
            UnitType.Standard,
            Quantity: 1,
            trailerNumber,
            IsBillable: true,
            TestOrders.Photos(1),
            CommentCount: 0);

        Assert.ThrowsAny<ArgumentException>(() => order.RecordOperation(shipmentId, operation, TestOrders.At(1)));
    }

    [Theory]
    [InlineData(OperationType.Disposal)]
    [InlineData(OperationType.Restack)]
    [InlineData(OperationType.Loading)]
    public void OperationBeyondOnStockIsRefused(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Guid shipmentId = order.Shipments.Single().Id;
        NewOperation operation = TestOrders.OperationOf(type, TestOrders.DeclaredStandard + 1);

        Result<Operation> result = order.RecordOperation(shipmentId, operation, TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "operation.exceeds_stock");
    }

    [Theory]
    [InlineData(OperationType.Disposal)]
    [InlineData(OperationType.Restack)]
    [InlineData(OperationType.Loading)]
    public void OperationRefusedForStockLeavesTheOrderUnchanged(OperationType type)
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();
        DateTimeOffset updatedAtBefore = order.UpdatedAt;
        int versionBefore = order.Version;
        NewOperation operation = TestOrders.OperationOf(type, TestOrders.DeclaredStandard + 1, photoCount: 0);

        order.RecordOperation(shipment.Id, operation, TestOrders.At(1));

        Assert.Equal(updatedAtBefore, order.UpdatedAt);
        Assert.Equal(versionBefore, order.Version);
        Assert.Single(shipment.Operations);
        Assert.Equal(TestOrders.DeclaredStandard, shipment.OnStock(UnitType.Standard));
        Assert.Empty(shipment.Alerts);
    }

    [Fact]
    public void OperationRefusedForStockDoesNotStartAReadyOrder()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;

        order.RecordOperation(shipmentId, TestOrders.OperationOf(OperationType.Loading, 1), TestOrders.At(1));

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Null(order.StartedAt);
    }

    [Fact]
    public void OnStockIsCountedPerShipmentAndUnitType()
    {
        Order order = TestOrders.Ready(OrderType.Consolidation, declaredExtraLarge: 3);
        Shipment first = order.Shipments.First();
        Shipment second = order.Shipments.Last();

        RecordMixedLedger(order, first, second);

        Assert.Equal(8, first.OnStock(UnitType.Standard));
        Assert.Equal(2, first.OnStock(UnitType.ExtraLarge));
        Assert.Equal(4, second.OnStock(UnitType.Standard));
        Assert.Equal(0, second.OnStock(UnitType.ExtraLarge));
    }

    [Fact]
    public void ActualQuantityIsCountedPerShipmentAndUnitType()
    {
        Order order = TestOrders.Ready(OrderType.Consolidation, declaredExtraLarge: 3);
        Shipment first = order.Shipments.First();
        Shipment second = order.Shipments.Last();

        RecordMixedLedger(order, first, second);

        Assert.Equal(10, first.ActualQuantity(UnitType.Standard));
        Assert.Equal(3, first.ActualQuantity(UnitType.ExtraLarge));
        Assert.Equal(4, second.ActualQuantity(UnitType.Standard));
        Assert.Equal(0, second.ActualQuantity(UnitType.ExtraLarge));
    }

    [Fact]
    public void RestackLeavesOnStockUnchanged()
    {
        Order order = TestOrders.Unloaded();
        Shipment shipment = order.Shipments.Single();

        order.RecordOperation(shipment.Id, TestOrders.OperationOf(OperationType.Restack, 4), TestOrders.At(1))
            .MustSucceed();

        Assert.Equal(TestOrders.DeclaredStandard, shipment.OnStock(UnitType.Standard));
    }

    [Fact]
    public void InstantWithAnOffsetAndSubMicrosecondTicksIsStoredInUtcAndWholeMicroseconds()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        DateTimeOffset hubLocal = new DateTimeOffset(2026, 3, 2, 5, 30, 0, TimeSpan.FromHours(-5)).AddTicks(15);
        DateTimeOffset expected = new DateTimeOffset(2026, 3, 2, 10, 30, 0, TimeSpan.Zero).AddTicks(10);
        NewOperation unloadingWithoutPhoto = TestOrders.OperationOf(OperationType.Unloading, 1, photoCount: 0);

        Operation operation = order.RecordOperation(shipment.Id, unloadingWithoutPhoto, hubLocal).MustSucceed();

        Alert alert = Assert.Single(shipment.Alerts);
        DateTimeOffset?[] storedInstants = [operation.AppliedAt, alert.RaisedAt, order.StartedAt, order.UpdatedAt];
        Assert.All(storedInstants, storedInstant =>
        {
            Assert.Equal(expected, storedInstant);
            Assert.Equal(TimeSpan.Zero, storedInstant?.Offset);
        });
    }

    [Fact]
    public void DraftInstantWithAnOffsetIsStoredInUtc()
    {
        var hubLocal = new DateTimeOffset(2026, 3, 2, 5, 30, 0, TimeSpan.FromHours(-5));

        Order order = Order.StartDraft(
            TestOrders.DraftNumber,
            OrderType.Consolidation,
            crossDockKind: null,
            TestOrders.Dispatcher(),
            Guid.CreateVersion7(),
            hubLocal);

        Assert.Equal(hubLocal, order.CreatedAt);
        Assert.Equal(TimeSpan.Zero, order.CreatedAt.Offset);
    }

    [Fact]
    public void ArrivalStoresTruckAndTrailerNumbersTrimmedAndUpperCased()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();

        order.RecordArrival(shipment.Id, TrailerType.Reefer53, " trk 7 ", " tr-9a ", TestOrders.At(1))
            .MustSucceed();

        Assert.Equal("TRK 7", shipment.TruckNumber);
        Assert.Equal("TR-9A", shipment.TrailerNumber);
    }

    [Fact]
    public void OperationStoresItsTrailerNumberTrimmedAndUpperCased()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;
        NewOperation unloading = TestOrders.OperationOf(OperationType.Unloading, 1) with { TrailerNumber = " tr-10b " };

        Operation operation = order.RecordOperation(shipmentId, unloading, TestOrders.At(1)).MustSucceed();

        Assert.Equal("TR-10B", operation.TrailerNumber);
    }

    [Fact]
    public void InstantEarlierThanUpdatedAtIsAContractViolation()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;
        order.SetWarehouseNote(shipmentId, "Fragile", TestOrders.At(5)).MustSucceed();

        Assert.ThrowsAny<ArgumentException>(() => order.SetWarehouseNote(shipmentId, "Keep dry", TestOrders.At(4)));
    }

    [Fact]
    public void ContractViolationOnACompletedOrderThrowsBeforeTheStatusRefusal()
    {
        Order order = TestOrders.Completed();
        Guid shipmentId = order.Shipments.Single().Id;

        Assert.ThrowsAny<ArgumentException>(
            () => order.RecordOperation(
                shipmentId,
                TestOrders.OperationOf(OperationType.Loading, 0),
                TestOrders.At(1)));
    }

    private static DomainError? Perform(Order order, HubWork work, DateTimeOffset now)
    {
        Guid shipmentId = order.Shipments.First().Id;
        return work switch
        {
            HubWork.Arrival => order.RecordArrival(shipmentId, TrailerType.DryVan53, "TRK-1", "TR-1", now).Error,
            HubWork.DockAssignment => order.AssignDock(shipmentId, "D4", "B2", "Alex Kim", now).Error,
            HubWork.Operation => order
                .RecordOperation(shipmentId, TestOrders.OperationOf(OperationType.Unloading, 1), now)
                .Error,
            HubWork.FinishReceiving => order.FinishReceiving(shipmentId, now).Error,
            HubWork.WarehouseNote => order.SetWarehouseNote(shipmentId, "Fragile", now).Error,
            _ => throw new ArgumentOutOfRangeException(nameof(work), work, null),
        };
    }

    private static void RecordMixedLedger(Order order, Shipment first, Shipment second)
    {
        Record(order, first, TestOrders.OperationOf(OperationType.Unloading, 10), TestOrders.At(1));
        Record(order, first, TestOrders.OperationOf(OperationType.Unloading, 3, UnitType.ExtraLarge), TestOrders.At(2));
        Record(order, first, TestOrders.OperationOf(OperationType.Disposal, 2), TestOrders.At(3));
        Record(order, first, TestOrders.OperationOf(OperationType.Loading, 1, UnitType.ExtraLarge), TestOrders.At(4));
        Record(order, second, TestOrders.OperationOf(OperationType.Unloading, 4), TestOrders.At(5));
    }

    private static void Record(Order order, Shipment shipment, NewOperation operation, DateTimeOffset now)
    {
        order.RecordOperation(shipment.Id, operation, now).MustSucceed();
    }

    public enum HubWork
    {
        Arrival = 1,
        DockAssignment = 2,
        Operation = 3,
        FinishReceiving = 4,
        WarehouseNote = 5,
    }
}
