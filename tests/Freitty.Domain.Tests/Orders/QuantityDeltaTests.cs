using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class QuantityDeltaTests
{
    [Fact]
    public void SplitUnloadBelowDeclaredRaisesNoQuantityDeltaAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 4, TestOrders.At(1));

        Unload(order, shipment, 3, TestOrders.At(2));

        Assert.Empty(QuantityDeltaAlerts(shipment));
    }

    [Fact]
    public void TwoUnitTypesUnloadedOneAfterTheOtherRaiseNoQuantityDeltaAlert()
    {
        Order order = TestOrders.Ready(declaredExtraLarge: 5);
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, TestOrders.DeclaredStandard, TestOrders.At(1));

        Unload(order, shipment, 5, TestOrders.At(2), UnitType.ExtraLarge);

        Assert.Empty(QuantityDeltaAlerts(shipment));
    }

    [Fact]
    public void UnloadingAboveDeclaredRaisesQuantityDeltaAlertAtOnce()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();

        Unload(order, shipment, 12, TestOrders.At(3));

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(3), alert.RaisedAt);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void UnloadingAnUndeclaredUnitTypeRaisesQuantityDeltaAlertAtOnce()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();

        Unload(order, shipment, 2, TestOrders.At(3), UnitType.ExtraLarge);

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void FinishingReceivingAShortShipmentRaisesQuantityDeltaAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 7, TestOrders.At(1));

        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(2), alert.RaisedAt);
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void FinishingReceivingAShipmentWithNoUnloadingRaisesQuantityDeltaAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();

        order.FinishReceiving(shipment.Id, TestOrders.At(1)).MustSucceed();

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Null(alert.ResolvedAt);
        Assert.True(shipment.HasQuantityDelta);
    }

    [Fact]
    public void FinishingReceivingWithMatchingQuantitiesRaisesNoAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, TestOrders.DeclaredStandard, TestOrders.At(1));

        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();

        Assert.Empty(shipment.Alerts);
        Assert.False(shipment.HasQuantityDelta);
    }

    [Fact]
    public void FinishingReceivingTwiceIsRefused()
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;
        order.FinishReceiving(shipmentId, TestOrders.At(1)).MustSucceed();

        Result result = order.FinishReceiving(shipmentId, TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "shipment.already_received");
    }

    [Fact]
    public void UnloadingThatEvensTheQuantitiesAfterReceivingResolvesTheAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 7, TestOrders.At(1));
        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();

        Unload(order, shipment, 3, TestOrders.At(3));

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(3), alert.ResolvedAt);
    }

    [Fact]
    public void UnloadingThatLeavesADeltaAfterReceivingKeepsASingleOpenAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 7, TestOrders.At(1));
        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();

        Unload(order, shipment, 1, TestOrders.At(3));

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Null(alert.ResolvedAt);
    }

    [Fact]
    public void QuantityDeltaOnOneShipmentRaisesNoAlertOnAnother()
    {
        Order order = TestOrders.Ready(OrderType.Consolidation);
        Shipment first = order.Shipments.First();
        Shipment second = order.Shipments.Last();

        Unload(order, first, 12, TestOrders.At(1));

        Assert.Single(QuantityDeltaAlerts(first));
        Assert.Empty(second.Alerts);
    }

    [Fact]
    public void FinishingReceivingWithTheSameExcessKeepsAResolvedAlertResolved()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 12, TestOrders.At(1));
        ResolveQuantityDeltaAlert(order, shipment, TestOrders.At(2));

        order.FinishReceiving(shipment.Id, TestOrders.At(3)).MustSucceed();

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(2), alert.ResolvedAt);
    }

    [Fact]
    public void LoadingKeepsAResolvedExcessAlertResolved()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 12, TestOrders.At(1));
        ResolveQuantityDeltaAlert(order, shipment, TestOrders.At(2));

        order.RecordOperation(shipment.Id, TestOrders.OperationOf(OperationType.Loading, 5), TestOrders.At(3))
            .MustSucceed();

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(2), alert.ResolvedAt);
    }

    [Fact]
    public void ExactUnloadingOfAnotherUnitTypeKeepsAResolvedExcessAlertResolved()
    {
        Order order = TestOrders.Ready(declaredExtraLarge: 5);
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 12, TestOrders.At(1));
        ResolveQuantityDeltaAlert(order, shipment, TestOrders.At(2));

        Unload(order, shipment, 5, TestOrders.At(3), UnitType.ExtraLarge);

        Alert alert = Assert.Single(QuantityDeltaAlerts(shipment));
        Assert.Equal(TestOrders.At(2), alert.ResolvedAt);
    }

    [Fact]
    public void UnloadingThatChangesTheDeltaRaisesANewAlertAfterManualResolution()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 12, TestOrders.At(1));
        ResolveQuantityDeltaAlert(order, shipment, TestOrders.At(2));

        Unload(order, shipment, 1, TestOrders.At(3));

        Assert.Equal(2, QuantityDeltaAlerts(shipment).Count());
        Alert openAlert = Assert.Single(QuantityDeltaAlerts(shipment), alert => alert.ResolvedAt is null);
        Assert.Equal(TestOrders.At(3), openAlert.RaisedAt);
    }

    [Fact]
    public void SmallerShortageAfterManualResolutionRaisesANewAlert()
    {
        Order order = TestOrders.Ready();
        Shipment shipment = order.Shipments.Single();
        Unload(order, shipment, 5, TestOrders.At(1));
        order.FinishReceiving(shipment.Id, TestOrders.At(2)).MustSucceed();
        ResolveQuantityDeltaAlert(order, shipment, TestOrders.At(3));

        Unload(order, shipment, 3, TestOrders.At(4));

        Alert openAlert = Assert.Single(QuantityDeltaAlerts(shipment), alert => alert.ResolvedAt is null);
        Assert.Equal(TestOrders.At(4), openAlert.RaisedAt);
    }

    private static void Unload(
        Order order,
        Shipment shipment,
        int quantity,
        DateTimeOffset now,
        UnitType unitType = UnitType.Standard)
    {
        NewOperation unloading = TestOrders.OperationOf(OperationType.Unloading, quantity, unitType);
        order.RecordOperation(shipment.Id, unloading, now).MustSucceed();
    }

    private static void ResolveQuantityDeltaAlert(Order order, Shipment shipment, DateTimeOffset now)
    {
        Alert alert = QuantityDeltaAlerts(shipment).Single(candidate => candidate.ResolvedAt is null);
        order.ResolveAlert(alert.Id, now).MustSucceed();
    }

    private static IEnumerable<Alert> QuantityDeltaAlerts(Shipment shipment)
    {
        return shipment.Alerts.Where(alert => alert.Type == AlertType.QuantityDelta);
    }
}
