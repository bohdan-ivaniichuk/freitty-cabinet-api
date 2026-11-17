using Freitty.Domain.Catalog;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class OrderChangeTests
{
    [Theory]
    [InlineData(Change.AddShipment)]
    [InlineData(Change.DeclareCargo)]
    [InlineData(Change.Schedule)]
    [InlineData(Change.SetDestination)]
    [InlineData(Change.ArrangeCarrier)]
    [InlineData(Change.ArrangeOwnDriver)]
    [InlineData(Change.ArrangeSelfPickup)]
    [InlineData(Change.AddService)]
    [InlineData(Change.Submit)]
    [InlineData(Change.RecordArrival)]
    [InlineData(Change.AssignDock)]
    [InlineData(Change.RecordOperation)]
    [InlineData(Change.FinishReceiving)]
    [InlineData(Change.SetWarehouseNote)]
    [InlineData(Change.ResolveAlert)]
    [InlineData(Change.Complete)]
    [InlineData(Change.TransferTo)]
    [InlineData(Change.AddSupply)]
    public void SuccessfulChangeSetsUpdatedAtToItsInstantAndAddsOneToVersion(Change change)
    {
        Order order = OrderReadyFor(change);
        int versionBefore = order.Version;

        DomainError? error = Apply(order, change, FirstShipmentId(order), TestOrders.At(30));

        Assert.Null(error);
        Assert.Equal(TestOrders.At(30), order.UpdatedAt);
        Assert.Equal(versionBefore + 1, order.Version);
    }

    [Fact]
    public void ChangeAtTheInstantOfThePreviousOneStillAddsOneToVersion()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = FirstShipmentId(order);
        order.SetWarehouseNote(shipmentId, "Fragile", TestOrders.At(1)).MustSucceed();
        int versionBefore = order.Version;

        order.SetWarehouseNote(shipmentId, "Keep dry", TestOrders.At(1)).MustSucceed();

        Assert.Equal(versionBefore + 1, order.Version);
    }

    [Theory]
    [InlineData(Change.DeclareCargo)]
    [InlineData(Change.RecordArrival)]
    [InlineData(Change.AssignDock)]
    [InlineData(Change.RecordOperation)]
    [InlineData(Change.FinishReceiving)]
    [InlineData(Change.SetWarehouseNote)]
    public void UnknownShipmentIsRefusedAsNotFound(Change change)
    {
        Order order = OrderReadyFor(change);

        DomainError? error = Apply(order, change, Guid.CreateVersion7(), TestOrders.At(1));

        AssertRefusal.NotFound(error, "shipment.not_found");
    }

    private static Order OrderReadyFor(Change change)
    {
        return change switch
        {
            Change.AddShipment => TestOrders.Draft(),
            Change.DeclareCargo
                or Change.Schedule
                or Change.SetDestination
                or Change.ArrangeCarrier
                or Change.ArrangeOwnDriver
                or Change.ArrangeSelfPickup
                or Change.AddService
                or Change.Submit => TestOrders.SubmittableDraft(),
            Change.ResolveAlert => OrderWithOpenAlert(),
            Change.Complete => TestOrders.ReadyToComplete(),
            _ => TestOrders.Ready(),
        };
    }

    private static Order OrderWithOpenAlert()
    {
        Order order = TestOrders.Ready();
        NewOperation unloadingWithoutPhoto = TestOrders.OperationOf(OperationType.Unloading, 1, photoCount: 0);
        order.RecordOperation(FirstShipmentId(order), unloadingWithoutPhoto, TestOrders.Start).MustSucceed();
        return order;
    }

    private static Guid FirstShipmentId(Order order)
    {
        return order.Shipments.Select(shipment => shipment.Id).FirstOrDefault();
    }

    private static DomainError? Apply(Order order, Change change, Guid shipmentId, DateTimeOffset now)
    {
        return change switch
        {
            Change.AddShipment => order.AddShipment("REF-1", now).Error,
            Change.DeclareCargo => order.DeclareCargo(shipmentId, UnitType.ExtraLarge, 5, now).Error,
            Change.Schedule => order.Schedule(now.AddDays(2), now).Error,
            Change.SetDestination => order.SetDestination("Montreal", "QC", now).Error,
            Change.ArrangeCarrier => order.ArrangeCarrier(Guid.CreateVersion7(), now).Error,
            Change.ArrangeOwnDriver => order.ArrangeOwnDriver("Sam Lee", now).Error,
            Change.ArrangeSelfPickup => order.ArrangeSelfPickup(now).Error,
            Change.AddService => order.AddService(Guid.CreateVersion7(), now).Error,
            Change.Submit => order.Submit(TestOrders.OrderNumber, now).Error,
            Change.RecordArrival => order.RecordArrival(shipmentId, TrailerType.DryVan53, "TRK-1", "TR-1", now).Error,
            Change.AssignDock => order.AssignDock(shipmentId, "D4", "B2", "Alex Kim", now).Error,
            Change.RecordOperation => order
                .RecordOperation(shipmentId, TestOrders.OperationOf(OperationType.Unloading, 1), now)
                .Error,
            Change.FinishReceiving => order.FinishReceiving(shipmentId, now).Error,
            Change.SetWarehouseNote => order.SetWarehouseNote(shipmentId, "Fragile", now).Error,
            Change.ResolveAlert => order.ResolveAlert(OpenAlertId(order), now).Error,
            Change.Complete => order.Complete(now).Error,
            Change.TransferTo => order.TransferTo(Guid.CreateVersion7(), now).Error,
            Change.AddSupply => order.AddSupply(new SupplyItem("WRAP-01", "Packaging", 4.50m), 2, now).Error,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
    }

    private static Guid OpenAlertId(Order order)
    {
        return order.Shipments
            .SelectMany(shipment => shipment.Alerts)
            .Single(alert => alert.ResolvedAt is null)
            .Id;
    }

    public enum Change
    {
        AddShipment = 1,
        DeclareCargo = 2,
        Schedule = 3,
        SetDestination = 4,
        ArrangeCarrier = 5,
        ArrangeOwnDriver = 6,
        ArrangeSelfPickup = 7,
        AddService = 8,
        Submit = 9,
        RecordArrival = 10,
        AssignDock = 11,
        RecordOperation = 12,
        FinishReceiving = 13,
        SetWarehouseNote = 14,
        ResolveAlert = 15,
        Complete = 16,
        TransferTo = 17,
        AddSupply = 18,
    }
}
