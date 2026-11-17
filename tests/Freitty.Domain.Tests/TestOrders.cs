using Freitty.Domain.Companies;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests;

// Builds orders through the public API only. Every setup step happens at Start, so a test acts at At(1) and later.
internal static class TestOrders
{
    public const string DraftNumber = "DRAFT-001";
    public const string OrderNumber = "FR001383";
    public const string TrailerNumber = "TR-100";
    public const int DeclaredStandard = 10;

    public static readonly DateTimeOffset Start = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset At(int minutes)
    {
        return Start.AddMinutes(minutes);
    }

    public static User Dispatcher()
    {
        var company = new Company("Northwind Freight", balance: 0m);
        return new User(company.Id, "Dana Smith", UserRole.Dispatcher);
    }

    public static Order Draft(OrderType type = OrderType.CrossDock)
    {
        CrossDockKind? crossDockKind = type == OrderType.CrossDock ? CrossDockKind.Storage : null;
        return Order.StartDraft(DraftNumber, type, crossDockKind, Dispatcher(), Guid.CreateVersion7(), Start);
    }

    // One shipment for a Cross-Dock, two for a Consolidation; each declares DeclaredStandard Standard pallets and,
    // when asked, ExtraLarge ones.
    public static Order SubmittableDraft(OrderType type = OrderType.CrossDock, int declaredExtraLarge = 0)
    {
        Order order = Draft(type);
        int shipmentCount = type == OrderType.CrossDock ? 1 : 2;
        for (int position = 1; position <= shipmentCount; position++)
        {
            Shipment shipment = order.AddShipment($"REF-{position}", Start).MustSucceed();
            order.DeclareCargo(shipment.Id, UnitType.Standard, DeclaredStandard, Start).MustSucceed();
            if (declaredExtraLarge > 0)
            {
                order.DeclareCargo(shipment.Id, UnitType.ExtraLarge, declaredExtraLarge, Start).MustSucceed();
            }
        }

        order.Schedule(Start.AddDays(1), Start).MustSucceed();
        order.SetDestination("Toronto", "ON", Start).MustSucceed();
        order.ArrangeSelfPickup(Start).MustSucceed();
        return order;
    }

    public static Order Ready(OrderType type = OrderType.CrossDock, int declaredExtraLarge = 0)
    {
        Order order = SubmittableDraft(type, declaredExtraLarge);
        order.Submit(OrderNumber, Start).MustSucceed();
        return order;
    }

    // In progress, with the declared Standard pallets of every shipment unloaded with a photo and on stock.
    public static Order Unloaded(OrderType type = OrderType.CrossDock)
    {
        Order order = Ready(type);
        foreach (Shipment shipment in order.Shipments)
        {
            order.RecordOperation(shipment.Id, OperationOf(OperationType.Unloading, DeclaredStandard), Start)
                .MustSucceed();
        }

        return order;
    }

    public static Order ReadyToComplete()
    {
        Order order = Unloaded();
        foreach (Shipment shipment in order.Shipments)
        {
            order.FinishReceiving(shipment.Id, Start).MustSucceed();
            order.RecordOperation(shipment.Id, OperationOf(OperationType.Loading, DeclaredStandard), Start)
                .MustSucceed();
        }

        return order;
    }

    public static Order Completed()
    {
        Order order = ReadyToComplete();
        order.Complete(Start).MustSucceed();
        return order;
    }

    public static Order InStatus(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Draft => SubmittableDraft(),
            OrderStatus.Ready => Ready(),
            OrderStatus.InProgress => Unloaded(),
            OrderStatus.Completed => Completed(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    public static NewOperation OperationOf(
        OperationType type,
        int quantity,
        UnitType unitType = UnitType.Standard,
        int photoCount = 1)
    {
        string? trailerNumber = type is OperationType.Unloading or OperationType.Loading ? TrailerNumber : null;
        return new NewOperation(type, unitType, quantity, trailerNumber, IsBillable: true, photoCount, CommentCount: 0);
    }
}
