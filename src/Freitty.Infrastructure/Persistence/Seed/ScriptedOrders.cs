using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// The ten orders the screens and the demo rely on, one method each. At(day, hour, minute) is a local time on the
// anchor day plus that many days: At(-2, 15, 00) is 15:00 two days before it, At(1, ...) the seeding day.
internal sealed class ScriptedOrders(ReferenceData reference, SeedCalendar calendar)
{
    public IReadOnlyList<OrderScript> Build()
    {
        return
        [
            CrossDockBeingLoaded(),
            ConsolidationPartlyLoaded(),
            ConsolidationWithOpenAlerts(),
            CrossDockOnStock(),
            PickupJustArrived(),
            ReadyForSeedingDay(),
            ReadyLater(),
            PaidPickup(),
            UnpaidTransferredConsolidation(),
            UntouchedDraft(),
        ];
    }

    private OrderScript CrossDockBeingLoaded()
    {
        OrderScript order = CrossDock(CrossDockKind.Storage, reference.User2, reference.Markham, At(-2, 15, 00));
        Shipment shipment = order.AddShipment("REF-1012", (UnitType.Standard, 10));
        order.Schedule(At(0, 8, 30));
        order.SetDestination("Kingston", "ON");
        order.ArrangeCarrier(reference.RWayTransport);
        order.AddServices(reference.Transload, reference.RestockAndRework);
        order.Submit("FR001383", At(-2, 15, 20));

        order.Arrive(shipment, TrailerType.DryVan53, "TRK-4521", "TRL-8830", At(0, 8, 40), dock: "12", bay: "B");
        order.Unload(shipment, UnitType.Standard, 12, photos: 4, At(0, 8, 55));
        order.SetWarehouseNote(
            shipment,
            "Counted 12 pallets on arrival, BOL says 10. 1 pallet damaged → routed to Disposal.",
            At(0, 9, 00));
        order.FinishReceiving(shipment, At(0, 9, 05));
        order.DisposeOf(shipment, UnitType.Standard, 1, photos: 2, comments: 1, At(0, 9, 10));
        order.Restack(shipment, UnitType.Standard, 11, photos: 1, At(0, 9, 25));
        order.AddSupply(reference.SupplyItem("Straps 12"), 4, At(0, 9, 30));
        order.AddSupply(reference.SupplyItem("Corners 50"), 16, At(0, 9, 30));
        order.AddSupply(reference.SupplyItem("Shrink wrap 120g"), 2, At(0, 9, 30));
        order.Load(shipment, UnitType.Standard, 6, "TRL-8830", At(0, 10, 40));
        return order;
    }

    private OrderScript ConsolidationPartlyLoaded()
    {
        OrderScript order = Consolidation(reference.User1, reference.Markham, At(-7, 10, 00));
        Shipment first = order.AddShipment("REF-1001", (UnitType.Standard, 9));
        Shipment second = order.AddShipment("REF-1003", (UnitType.Standard, 6));
        Shipment third = order.AddShipment("REF-1002", (UnitType.ExtraLarge, 3));
        order.Schedule(At(-5, 9, 00));
        order.SetDestination("Toronto", "ON");
        order.ArrangeOwnDriver(ReferenceData.OwnDriver);
        order.Submit("FR001676", At(-7, 10, 30));

        order.Arrive(first, TrailerType.DryVan53, "TRK-2210", "TRL-5102", At(-5, 9, 10), dock: "3", bay: "A");
        order.Unload(first, UnitType.Standard, 9, photos: 2, At(-5, 9, 30));
        order.FinishReceiving(first, At(-5, 9, 45));
        order.Arrive(second, TrailerType.DryVan53, "TRK-2214", "TRL-5117", At(-5, 13, 00), dock: "4", bay: "A");
        order.Unload(second, UnitType.Standard, 6, photos: 1, At(-5, 13, 20));
        order.FinishReceiving(second, At(-5, 13, 30));
        order.Arrive(third, TrailerType.Flatbed48, "TRK-3075", "TRL-6140", At(-4, 10, 00), dock: "7", bay: "C");
        order.Unload(third, UnitType.ExtraLarge, 3, photos: 1, At(-4, 10, 25));
        order.FinishReceiving(third, At(-4, 10, 35));

        order.Load(first, UnitType.Standard, 9, "TRL-7001", At(-1, 15, 00));
        order.Load(second, UnitType.Standard, 4, "TRL-7001", At(-1, 15, 20));
        order.Load(third, UnitType.ExtraLarge, 3, "TRL-7002", At(0, 9, 30));
        return order;
    }

    private OrderScript ConsolidationWithOpenAlerts()
    {
        OrderScript order = Consolidation(reference.User3, reference.Markham, At(-7, 9, 00));
        Shipment first = order.AddShipment("REF-1005", (UnitType.Standard, 11));
        Shipment second = order.AddShipment("REF-1006", (UnitType.Standard, 9));
        order.Schedule(At(-4, 11, 00));
        order.SetDestination("Calgary", "AB");
        order.ArrangeCarrier(reference.TForce);
        order.Submit("FR001674", At(-7, 9, 20));

        order.Arrive(first, TrailerType.DryVan53, "TRK-1880", "TRL-4410", At(-4, 11, 05), dock: "5", bay: "A");
        order.Unload(first, UnitType.Standard, 11, photos: 2, At(-4, 11, 30));
        order.FinishReceiving(first, At(-4, 11, 40));
        order.Arrive(second, TrailerType.DryVan53, "TRK-1893", "TRL-4432", At(-3, 14, 00), dock: "5", bay: "B");
        order.Unload(second, UnitType.Standard, 7, photos: 0, At(-3, 14, 25));
        order.FinishReceiving(second, At(-3, 14, 40));

        order.Load(first, UnitType.Standard, 11, "TRL-7003", At(-2, 10, 00));
        return order;
    }

    private OrderScript CrossDockOnStock()
    {
        OrderScript order = CrossDock(CrossDockKind.Storage, reference.User4, reference.Toronto, At(-5, 11, 00));
        Shipment shipment = order.AddShipment("REF-1013", (UnitType.Standard, 14));
        order.Schedule(At(-1, 10, 00));
        order.SetDestination("Windsor", "ON");
        order.ArrangeCarrier(reference.Schneider);
        order.Submit("FR001677", At(-5, 11, 15));

        order.Arrive(shipment, TrailerType.Reefer53, "TRK-5521", "TRL-9034", At(-1, 10, 10), dock: "2", bay: "A");
        order.Unload(shipment, UnitType.Standard, 14, photos: 3, At(-1, 10, 40));
        order.FinishReceiving(shipment, At(-1, 10, 50));
        return order;
    }

    private OrderScript PickupJustArrived()
    {
        OrderScript order = CrossDock(CrossDockKind.Pickup, reference.User3, reference.Markham, At(-4, 14, 00));
        Shipment shipment = order.AddShipment("REF-1014", (UnitType.ExtraLarge, 8));
        order.Schedule(At(0, 13, 00));
        order.SetDestination("Hamilton", "ON");
        order.ArrangeSelfPickup();
        order.Submit("FR001678", At(-4, 14, 10));

        order.Arrive(shipment, TrailerType.Flatbed48, "TRK-6102", "TRL-9120", At(0, 13, 10), dock: "9", bay: "C");
        return order;
    }

    private OrderScript ReadyForSeedingDay()
    {
        OrderScript order = CrossDock(CrossDockKind.Storage, reference.User1, reference.Markham, At(-3, 9, 00));
        order.AddShipment("REF-1015", (UnitType.Standard, 18), (UnitType.ExtraLarge, 2));
        order.Schedule(At(1, 10, 00));
        order.SetDestination("Montreal", "QC");
        order.ArrangeCarrier(reference.TForce);
        order.AddServices(reference.Transload);
        order.Submit("FR001679", At(-3, 9, 15));
        return order;
    }

    private OrderScript ReadyLater()
    {
        OrderScript order = CrossDock(CrossDockKind.Storage, reference.User2, reference.Toronto, At(-1, 16, 00));
        order.AddShipment("REF-1004", (UnitType.Standard, 23));
        order.Schedule(At(2, 14, 00));
        order.SetDestination("Detroit", "MI");
        order.ArrangeCarrier(reference.Schneider);
        order.Submit("FR001681", At(-1, 16, 15));
        return order;
    }

    private OrderScript PaidPickup()
    {
        OrderScript order = CrossDock(CrossDockKind.Pickup, reference.User1, reference.Markham, At(-9, 10, 00));
        Shipment shipment = order.AddShipment("REF-1007", (UnitType.ExtraLarge, 10));
        order.Schedule(At(-3, 17, 30));
        order.SetDestination("Brampton", "ON");
        order.ArrangeSelfPickup();
        order.Submit("FR001672", At(-9, 10, 10));

        order.Arrive(shipment, TrailerType.Flatbed48, "TRK-4407", "TRL-8215", At(-3, 17, 35), dock: "8", bay: "B");
        order.Unload(shipment, UnitType.ExtraLarge, 10, photos: 2, At(-3, 17, 55));
        order.FinishReceiving(shipment, At(-3, 18, 05));
        order.Load(shipment, UnitType.ExtraLarge, 10, "TRL-8301", At(-2, 9, 30));
        order.Complete(At(-2, 9, 45));
        order.Invoice(At(-2, 10, 00), paidAt: At(-1, 12, 00));
        return order;
    }

    private OrderScript UnpaidTransferredConsolidation()
    {
        OrderScript order = Consolidation(reference.User4, reference.Markham, At(-10, 9, 00));
        Shipment[] shipments =
        [
            order.AddShipment("REF-1008", (UnitType.Standard, 15)),
            order.AddShipment("REF-1009", (UnitType.Standard, 8)),
            order.AddShipment("REF-1010", (UnitType.Standard, 20)),
            order.AddShipment("REF-1011", (UnitType.Standard, 12)),
        ];
        order.Schedule(At(-6, 8, 00));
        order.SetDestination("Ottawa", "ON");
        order.ArrangeCarrier(reference.TForce);
        order.Submit("FR001668", At(-10, 9, 30));
        order.TransferTo(reference.Toronto, At(-9, 11, 00));

        order.Arrive(shipments[0], TrailerType.DryVan53, "TRK-3301", "TRL-6201", At(-6, 8, 10), dock: "1", bay: "A");
        order.Unload(shipments[0], UnitType.Standard, 15, photos: 1, At(-6, 8, 30));
        order.FinishReceiving(shipments[0], At(-6, 8, 40));
        order.Arrive(shipments[1], TrailerType.DryVan53, "TRK-3302", "TRL-6202", At(-6, 9, 40), dock: "2", bay: "A");
        order.Unload(shipments[1], UnitType.Standard, 8, photos: 1, At(-6, 10, 00));
        order.FinishReceiving(shipments[1], At(-6, 10, 10));
        order.Arrive(shipments[2], TrailerType.DryVan53, "TRK-3303", "TRL-6203", At(-6, 11, 10), dock: "3", bay: "A");
        order.Unload(shipments[2], UnitType.Standard, 20, photos: 1, At(-6, 11, 30));
        order.FinishReceiving(shipments[2], At(-6, 11, 40));
        order.Arrive(shipments[3], TrailerType.DryVan53, "TRK-3304", "TRL-6204", At(-6, 13, 10), dock: "4", bay: "A");
        order.Unload(shipments[3], UnitType.Standard, 12, photos: 1, At(-6, 13, 30));
        order.FinishReceiving(shipments[3], At(-6, 13, 40));

        order.Load(shipments[0], UnitType.Standard, 15, "TRL-7004", At(-5, 10, 00));
        order.Load(shipments[1], UnitType.Standard, 8, "TRL-7004", At(-5, 10, 15));
        order.Load(shipments[2], UnitType.Standard, 20, "TRL-7005", At(-5, 10, 30));
        order.Load(shipments[3], UnitType.Standard, 12, "TRL-7005", At(-5, 10, 45));
        order.Complete(At(-5, 16, 00));
        order.Invoice(At(-5, 16, 30), paidAt: null);
        return order;
    }

    private OrderScript UntouchedDraft()
    {
        OrderScript order = Consolidation(reference.User1, reference.Markham, At(-1, 16, 30), draftNumber: "DRAFT-003");
        order.Schedule(At(3, 9, 00));
        return order;
    }

    private OrderScript CrossDock(CrossDockKind kind, User createdBy, Hub hub, DateTimeOffset draftedAt)
    {
        return OrderScript.Draft(OrderType.CrossDock, kind, createdBy, hub, reference.FloorLeadOf(hub), draftedAt);
    }

    private OrderScript Consolidation(User createdBy, Hub hub, DateTimeOffset draftedAt, string? draftNumber = null)
    {
        return OrderScript.Draft(
            OrderType.Consolidation,
            crossDockKind: null,
            createdBy,
            hub,
            reference.FloorLeadOf(hub),
            draftedAt,
            draftNumber);
    }

    private DateTimeOffset At(int dayOffset, int hour, int minute)
    {
        return calendar.At(dayOffset, hour, minute);
    }
}
