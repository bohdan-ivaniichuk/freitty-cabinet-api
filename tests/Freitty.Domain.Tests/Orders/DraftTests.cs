using Freitty.Domain.Common;
using Freitty.Domain.Companies;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Orders;

public sealed class DraftTests
{
    [Fact]
    public void DraftStartsInDraftAtVersionOneWithItsInstantsSet()
    {
        User dispatcher = TestOrders.Dispatcher();

        Order order = Order.StartDraft(
            TestOrders.DraftNumber,
            OrderType.CrossDock,
            CrossDockKind.Pickup,
            dispatcher,
            Guid.CreateVersion7(),
            TestOrders.Start);

        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(1, order.Version);
        Assert.Equal(TestOrders.DraftNumber, order.Number);
        Assert.Equal(TestOrders.Start, order.CreatedAt);
        Assert.Equal(TestOrders.Start, order.UpdatedAt);
    }

    [Fact]
    public void DraftTakesItsCompanyFromTheUserWhoCreatedIt()
    {
        User dispatcher = TestOrders.Dispatcher();

        Order order = Order.StartDraft(
            TestOrders.DraftNumber,
            OrderType.Consolidation,
            crossDockKind: null,
            dispatcher,
            Guid.CreateVersion7(),
            TestOrders.Start);

        Assert.Equal(dispatcher.CompanyId, order.CompanyId);
        Assert.Equal(dispatcher.Id, order.CreatedById);
    }

    [Theory]
    [InlineData(OrderType.CrossDock, null)]
    [InlineData(OrderType.Consolidation, CrossDockKind.Storage)]
    public void CrossDockKindThatDoesNotMatchTheOrderTypeIsAContractViolation(
        OrderType type,
        CrossDockKind? crossDockKind)
    {
        User dispatcher = TestOrders.Dispatcher();

        Assert.ThrowsAny<ArgumentException>(() => Order.StartDraft(
            TestOrders.DraftNumber,
            type,
            crossDockKind,
            dispatcher,
            Guid.CreateVersion7(),
            TestOrders.Start));
    }

    [Fact]
    public void ShipmentsGetConsecutivePositions()
    {
        Order order = TestOrders.Draft(OrderType.Consolidation);

        order.AddShipment("REF-1", TestOrders.At(1)).MustSucceed();
        order.AddShipment("REF-2", TestOrders.At(2)).MustSucceed();
        order.AddShipment("REF-3", TestOrders.At(3)).MustSucceed();

        Assert.Equal(new[] { 1, 2, 3 }, order.Shipments.Select(shipment => shipment.Position));
    }

    [Fact]
    public void SecondShipmentOnACrossDockIsRefused()
    {
        Order order = TestOrders.Draft(OrderType.CrossDock);
        order.AddShipment("REF-1", TestOrders.At(1)).MustSucceed();

        Result<Shipment> result = order.AddShipment("REF-2", TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "shipment.cross_dock_has_one");
    }

    [Fact]
    public void RepeatedRefNumberIsRefused()
    {
        Order order = TestOrders.Draft(OrderType.Consolidation);
        order.AddShipment("REF-1", TestOrders.At(1)).MustSucceed();

        Result<Shipment> result = order.AddShipment("REF-1", TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "shipment.duplicate_ref_number");
    }

    [Fact]
    public void RepeatedUnitTypeOnOneShipmentIsRefused()
    {
        Order order = TestOrders.Draft();
        Shipment shipment = order.AddShipment("REF-1", TestOrders.At(1)).MustSucceed();
        order.DeclareCargo(shipment.Id, UnitType.Standard, 10, TestOrders.At(2)).MustSucceed();

        Result result = order.DeclareCargo(shipment.Id, UnitType.Standard, 5, TestOrders.At(3));

        AssertRefusal.Conflict(result.Error, "cargo_line.already_declared");
    }

    [Fact]
    public void RepeatedServiceIsRefused()
    {
        Order order = TestOrders.Draft();
        Guid serviceId = Guid.CreateVersion7();
        order.AddService(serviceId, TestOrders.At(1)).MustSucceed();

        Result result = order.AddService(serviceId, TestOrders.At(2));

        AssertRefusal.Conflict(result.Error, "service.already_added");
    }

    [Theory]
    [InlineData(DraftChange.AddShipment)]
    [InlineData(DraftChange.DeclareCargo)]
    [InlineData(DraftChange.Schedule)]
    [InlineData(DraftChange.SetDestination)]
    [InlineData(DraftChange.ArrangeCarrier)]
    [InlineData(DraftChange.ArrangeOwnDriver)]
    [InlineData(DraftChange.ArrangeSelfPickup)]
    [InlineData(DraftChange.AddService)]
    [InlineData(DraftChange.Submit)]
    public void DraftOnlyChangeIsRefusedOnceTheOrderIsSubmitted(DraftChange change)
    {
        Order order = TestOrders.Ready();
        Guid shipmentId = order.Shipments.Single().Id;

        DomainError? error = Apply(order, change, shipmentId, TestOrders.At(1));

        AssertRefusal.Conflict(error, "order.not_draft");
    }

    [Fact]
    public void DeclareCargoForAnUnknownShipmentOnASubmittedOrderReportsNotDraft()
    {
        Order order = TestOrders.Ready();

        Result result = order.DeclareCargo(Guid.CreateVersion7(), UnitType.ExtraLarge, 5, TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.not_draft");
    }

    [Theory]
    [InlineData(SubmitPart.Schedule, "a scheduled date")]
    [InlineData(SubmitPart.Destination, "a destination")]
    [InlineData(SubmitPart.Transport, "a transport arrangement")]
    [InlineData(SubmitPart.Shipment, "a shipment")]
    [InlineData(SubmitPart.Cargo, "cargo on DRAFT-001-1")]
    [InlineData(SubmitPart.SecondShipment, "at least 2 shipments")]
    public void SubmitIsRefusedForEachMissingPart(SubmitPart missingPart, string expectedMention)
    {
        Order order = DraftWithout(missingPart);

        Result result = order.Submit(TestOrders.OrderNumber, TestOrders.At(1));

        DomainError error = AssertRefusal.Conflict(result.Error, "order.not_ready_to_submit");
        Assert.Contains(expectedMention, error.Message);
    }

    [Fact]
    public void SubmitRefusalListsEveryMissingPart()
    {
        Order order = TestOrders.Draft(OrderType.Consolidation);
        order.AddShipment("REF-1", TestOrders.At(1)).MustSucceed();

        Result result = order.Submit(TestOrders.OrderNumber, TestOrders.At(2));

        DomainError error = AssertRefusal.Conflict(result.Error, "order.not_ready_to_submit");
        Assert.Contains("a scheduled date", error.Message);
        Assert.Contains("a destination", error.Message);
        Assert.Contains("a transport arrangement", error.Message);
        Assert.Contains("at least 2 shipments", error.Message);
        Assert.Contains("cargo on DRAFT-001-1", error.Message);
    }

    [Fact]
    public void SubmitTellsAConsolidationWithoutShipmentsThatItNeedsTwo()
    {
        Order order = TestOrders.Draft(OrderType.Consolidation);

        Result result = order.Submit(TestOrders.OrderNumber, TestOrders.At(1));

        DomainError error = AssertRefusal.Conflict(result.Error, "order.not_ready_to_submit");
        Assert.Contains("at least 2 shipments", error.Message);
    }

    [Fact]
    public void SubmitSetsTheOfficialNumberReadyAndSubmittedAt()
    {
        Order order = TestOrders.SubmittableDraft();

        Result result = order.Submit("FR001400", TestOrders.At(5));

        Assert.True(result.IsSuccess);
        Assert.Equal("FR001400", order.Number);
        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Equal(TestOrders.At(5), order.SubmittedAt);
    }

    [Fact]
    public void ArrangingOwnDriverClearsTheCarrier()
    {
        Order order = TestOrders.Draft();
        order.ArrangeCarrier(Guid.CreateVersion7(), TestOrders.At(1)).MustSucceed();

        order.ArrangeOwnDriver("Sam Lee", TestOrders.At(2)).MustSucceed();

        Assert.Equal(TransportKind.OwnDriver, order.TransportKind);
        Assert.Equal("Sam Lee", order.DriverName);
        Assert.Null(order.CarrierId);
    }

    [Fact]
    public void ArrangingCarrierClearsTheDriver()
    {
        Order order = TestOrders.Draft();
        Guid carrierId = Guid.CreateVersion7();
        order.ArrangeOwnDriver("Sam Lee", TestOrders.At(1)).MustSucceed();

        order.ArrangeCarrier(carrierId, TestOrders.At(2)).MustSucceed();

        Assert.Equal(TransportKind.Carrier, order.TransportKind);
        Assert.Equal(carrierId, order.CarrierId);
        Assert.Null(order.DriverName);
    }

    [Fact]
    public void ArrangingSelfPickupClearsTheCarrierAndTheDriver()
    {
        Order order = TestOrders.Draft();
        order.ArrangeCarrier(Guid.CreateVersion7(), TestOrders.At(1)).MustSucceed();
        order.ArrangeOwnDriver("Sam Lee", TestOrders.At(2)).MustSucceed();

        order.ArrangeSelfPickup(TestOrders.At(3)).MustSucceed();

        Assert.Equal(TransportKind.SelfPickup, order.TransportKind);
        Assert.Null(order.CarrierId);
        Assert.Null(order.DriverName);
    }

    [Fact]
    public void TransferToWorksOnASubmittedOrder()
    {
        Order order = TestOrders.Ready();
        Guid transferHubId = Guid.CreateVersion7();

        Result result = order.TransferTo(transferHubId, TestOrders.At(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(transferHubId, order.TransferHubId);
    }

    [Fact]
    public void TransferToDoesNotStartAReadyOrder()
    {
        Order order = TestOrders.Ready();

        order.TransferTo(Guid.CreateVersion7(), TestOrders.At(1)).MustSucceed();

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Null(order.StartedAt);
    }

    [Fact]
    public void TransferToTheOrdersOwnHubIsRefused()
    {
        Order order = TestOrders.Ready();

        Result result = order.TransferTo(order.HubId, TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.same_hub");
    }

    [Fact]
    public void TransferToIsRefusedOnACompletedOrder()
    {
        Order order = TestOrders.Completed();

        Result result = order.TransferTo(Guid.CreateVersion7(), TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "order.already_completed");
    }

    private static DomainError? Apply(Order order, DraftChange change, Guid shipmentId, DateTimeOffset now)
    {
        return change switch
        {
            DraftChange.AddShipment => order.AddShipment("REF-9", now).Error,
            DraftChange.DeclareCargo => order.DeclareCargo(shipmentId, UnitType.ExtraLarge, 5, now).Error,
            DraftChange.Schedule => order.Schedule(now.AddDays(2), now).Error,
            DraftChange.SetDestination => order.SetDestination("Montreal", "QC", now).Error,
            DraftChange.ArrangeCarrier => order.ArrangeCarrier(Guid.CreateVersion7(), now).Error,
            DraftChange.ArrangeOwnDriver => order.ArrangeOwnDriver("Sam Lee", now).Error,
            DraftChange.ArrangeSelfPickup => order.ArrangeSelfPickup(now).Error,
            DraftChange.AddService => order.AddService(Guid.CreateVersion7(), now).Error,
            DraftChange.Submit => order.Submit("FR009999", now).Error,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
    }

    private static Order DraftWithout(SubmitPart missingPart)
    {
        OrderType type = missingPart == SubmitPart.SecondShipment ? OrderType.Consolidation : OrderType.CrossDock;
        Order order = TestOrders.Draft(type);
        if (missingPart != SubmitPart.Shipment)
        {
            Shipment shipment = order.AddShipment("REF-1", TestOrders.Start).MustSucceed();
            if (missingPart != SubmitPart.Cargo)
            {
                order.DeclareCargo(shipment.Id, UnitType.Standard, 10, TestOrders.Start).MustSucceed();
            }
        }

        if (missingPart != SubmitPart.Schedule)
        {
            order.Schedule(TestOrders.Start.AddDays(1), TestOrders.Start).MustSucceed();
        }

        if (missingPart != SubmitPart.Destination)
        {
            order.SetDestination("Toronto", "ON", TestOrders.Start).MustSucceed();
        }

        if (missingPart != SubmitPart.Transport)
        {
            order.ArrangeSelfPickup(TestOrders.Start).MustSucceed();
        }

        return order;
    }

    public enum DraftChange
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
    }

    public enum SubmitPart
    {
        Schedule = 1,
        Destination = 2,
        Transport = 3,
        Shipment = 4,
        Cargo = 5,
        SecondShipment = 6,
    }
}
