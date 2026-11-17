using System.Globalization;
using Freitty.Domain.Catalog;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// Sixty completed orders for the dashboard figures and the chart, index 0 the oldest. Everything follows from the
// index of the order and from the running Ref N and vehicle counters, which Build starts over: the tables below
// are taken in turn, never at random, and each is taken so that it does not move in step with another.
internal sealed class OrderHistory(ReferenceData reference, SeedCalendar calendar)
{
    private const int FirstOrderNumber = 1301;
    private const int OrderNumberStep = 6;
    private const int FirstRefNumber = 901;
    private const int FirstVehicleNumber = 101;
    private const int ConsolidationEvery = 4;
    private const int TorontoEvery = 3;
    private const int CrossDockKindCount = 2;
    private const int MinConsolidationShipments = 2;
    private const int ConsolidationShipmentVariants = 3;
    private const int EarliestScheduledHour = 8;
    private const int ScheduledHourVariants = 7;
    private const int PhotoVariants = 3;
    private const int DockCount = 10;
    private const int DraftedDaysBeforeCompletion = 4;
    private const int ScheduledDaysBeforeCompletion = 1;
    private const int PaidDaysAfterCompletion = 2;

    private const string OrderNumberFormat = "FR{0:D6}";
    private const string RefNumberFormat = "REF-{0:D4}";
    private const string TruckNumberFormat = "TRK-{0:D4}";
    private const string TrailerNumberFormat = "TRL-{0:D4}";

    // Days before the anchor day on which the orders are completed, newest first, so order i takes the entry i
    // places from the end. With the two completed scripted orders it gives 24 completed in the last 30 days and 20
    // in the 30 before; the empty boundary days keep both counts on the seeding day and the next.
    private static readonly int[] CompletionDaysAgo =
    [
        3, 4, 6, 7, 8, 8, 10, 11, 12, 13, 14, 15, 17, 18, 20, 21, 22, 22, 24, 25, 26, 27,
        32, 34, 35, 36, 36, 38, 40, 41, 43, 44, 46, 48, 49, 50, 50, 52, 53, 55, 56, 57,
        62, 63, 65, 66, 68, 69, 71, 72, 74, 76, 77, 78, 79, 80, 81, 83, 88, 95,
    ];

    // Cross-Docks at both hubs whose Unloading differs from the declared quantity: an excess of one and a shortage
    // of two, alternating.
    private static readonly Dictionary<int, int> QuantityDeltas = new()
    {
        [5] = 1,
        [16] = -2,
        [29] = 1,
        [40] = -2,
        [53] = 1,
    };

    private static readonly TimeOnly DraftedTime = new(9, 0);
    private static readonly TimeOnly SubmittedTime = new(9, 30);
    private static readonly TimeOnly SuppliedTime = new(10, 0);
    private static readonly TimeOnly LoadedTime = new(11, 0);
    private static readonly TimeOnly CompletedTime = new(14, 0);
    private static readonly TimeOnly InvoicedTime = new(15, 0);
    private static readonly TimeOnly PaidTime = new(12, 0);

    private static readonly TimeSpan ArrivalAfterSchedule = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UnloadingAfterArrival = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ReceivingAfterUnloading = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan NextShipmentAfter = TimeSpan.FromMinutes(90);
    private static readonly TimeSpan AlertResolvedAfter = TimeSpan.FromMinutes(1);

    private static readonly TrailerType[] TrailerTypes =
        [TrailerType.DryVan53, TrailerType.Reefer53, TrailerType.Flatbed48];
    private static readonly string[] Bays = ["A", "B", "C"];

    private static readonly (string City, string Region)[] Destinations =
    [
        ("Ottawa", "ON"),
        ("Montreal", "QC"),
        ("Buffalo", "NY"),
        ("Kingston", "ON"),
        ("Detroit", "MI"),
        ("Quebec City", "QC"),
        ("Cleveland", "OH"),
    ];

    private static readonly (UnitType UnitType, int Quantity)[][] Cargo =
    [
        [(UnitType.Standard, 12)],
        [(UnitType.Standard, 8)],
        [(UnitType.ExtraLarge, 6)],
        [(UnitType.Standard, 16), (UnitType.ExtraLarge, 2)],
        [(UnitType.Standard, 10)],
        [(UnitType.Standard, 20)],
        [(UnitType.ExtraLarge, 4)],
    ];

    // Every other entry buys supplies, so about half of the orders do. Eleven entries share no factor with any
    // other cycle here, so buying is tied to no other attribute of the order.
    private static readonly (string Sku, int Quantity)[][] Supplies =
    [
        [("Straps 12", 4), ("Corners 50", 8)],
        [],
        [("Shrink wrap 120g", 2), ("Pallet cover", 6)],
        [],
        [("Load bar", 2), ("Air bag", 4)],
        [],
        [("Pallet 48x40", 5), ("Label roll 500", 1)],
        [],
        [("Stretch film hand roll", 3), ("Edge board 48in", 4)],
        [],
        [("Straps 24", 2), ("Slip sheet", 10)],
    ];

    private readonly Service[][] serviceSets =
    [
        [reference.Transload],
        [reference.RestockAndRework],
        [reference.Transload, reference.RestockAndRework],
    ];

    private readonly Action<OrderScript>[] transports =
    [
        script => script.ArrangeCarrier(reference.Schneider),
        script => script.ArrangeCarrier(reference.TForce),
        script => script.ArrangeOwnDriver(ReferenceData.OwnDriver),
        script => script.ArrangeCarrier(reference.RWayTransport),
        script => script.ArrangeSelfPickup(),
    ];

    private int shipmentCount;
    private int vehicleCount;

    public IReadOnlyList<OrderScript> Build()
    {
        shipmentCount = 0;
        vehicleCount = 0;
        var scripts = new List<OrderScript>();
        for (int index = 0; index < CompletionDaysAgo.Length; index++)
        {
            scripts.Add(BuildOrder(index));
        }

        return scripts;
    }

    // A table as long as the period of another attribute would repeat in step with it; stepping one entry further
    // every period orders breaks that lock while staying a plain function of the index.
    private static int Staggered(int index, int period)
    {
        return index + (index / period);
    }

    private static string Formatted(string format, int value)
    {
        return string.Format(CultureInfo.InvariantCulture, format, value);
    }

    private OrderScript BuildOrder(int index)
    {
        int completionDay = -CompletionDaysAgo[^(index + 1)];
        int scheduledHour =
            EarliestScheduledHour + (Staggered(index, ScheduledHourVariants) % ScheduledHourVariants);
        DateTimeOffset scheduledAt = calendar.At(completionDay - ScheduledDaysBeforeCompletion, scheduledHour, 0);
        OrderScript script = Submitted(index, completionDay, scheduledAt);

        int quantityDelta = QuantityDeltas.GetValueOrDefault(index);
        DateTimeOffset arrivedAt = scheduledAt + ArrivalAfterSchedule;
        foreach (Shipment shipment in script.Order.Shipments)
        {
            Receive(script, shipment, arrivedAt, quantityDelta);
            arrivedAt += NextShipmentAfter;
        }

        foreach ((string sku, int quantity) in Supplies[index % Supplies.Length])
        {
            script.AddSupply(reference.SupplyItem(sku), quantity, calendar.At(completionDay, SuppliedTime));
        }

        LoadEverything(script, calendar.At(completionDay, LoadedTime));
        script.Complete(calendar.At(completionDay, CompletedTime));
        script.Invoice(
            calendar.At(completionDay, InvoicedTime),
            paidAt: calendar.At(completionDay + PaidDaysAfterCompletion, PaidTime));
        return script;
    }

    private OrderScript Submitted(int index, int completionDay, DateTimeOffset scheduledAt)
    {
        DateTimeOffset draftedAt = calendar.At(completionDay - DraftedDaysBeforeCompletion, DraftedTime);
        Hub hub = index % TorontoEvery == TorontoEvery - 1 ? reference.Toronto : reference.Markham;
        bool isConsolidation = index % ConsolidationEvery == ConsolidationEvery - 1;
        OrderScript script = OrderScript.Draft(
            isConsolidation ? OrderType.Consolidation : OrderType.CrossDock,
            isConsolidation ? null : CrossDockKindOf(index),
            reference.Users[Staggered(index, ConsolidationEvery) % reference.Users.Count],
            hub,
            reference.FloorLeadOf(hub),
            draftedAt);

        int shipmentsInOrder = isConsolidation ? ConsolidationSizeOf(index) : 1;
        for (int position = 0; position < shipmentsInOrder; position++)
        {
            script.AddShipment(
                Formatted(RefNumberFormat, FirstRefNumber + shipmentCount),
                Cargo[shipmentCount % Cargo.Length]);
            shipmentCount++;
        }

        script.Schedule(scheduledAt);
        (string city, string region) = Destinations[index % Destinations.Length];
        script.SetDestination(city, region);
        transports[index % transports.Length](script);
        script.AddServices(serviceSets[Staggered(index, TorontoEvery) % serviceSets.Length]);

        script.Submit(
            Formatted(OrderNumberFormat, FirstOrderNumber + (OrderNumberStep * index)),
            calendar.At(completionDay - DraftedDaysBeforeCompletion, SubmittedTime));
        return script;
    }

    // Storage and Pickup alternate along the Cross-Docks alone: one Consolidation precedes every
    // ConsolidationEvery orders, so they are counted out.
    private static CrossDockKind CrossDockKindOf(int index)
    {
        int crossDocksBefore = index - (index / ConsolidationEvery);
        return crossDocksBefore % CrossDockKindCount == 0 ? CrossDockKind.Storage : CrossDockKind.Pickup;
    }

    private static int ConsolidationSizeOf(int index)
    {
        int consolidationsBefore = index / ConsolidationEvery;
        return MinConsolidationShipments + (consolidationsBefore % ConsolidationShipmentVariants);
    }

    // Trailer type, bay and photo count are three different digits of the vehicle number in base three, so all
    // their combinations occur. The delta goes to the first Cargo Line. Its alert is resolved a minute after it is
    // raised: an excess is known at the Unloading, a shortage only once receiving is finished.
    private void Receive(OrderScript script, Shipment shipment, DateTimeOffset arrivedAt, int quantityDelta)
    {
        int vehicle = NextVehicleNumber();
        script.Arrive(
            shipment,
            TrailerTypes[vehicle % TrailerTypes.Length],
            Formatted(TruckNumberFormat, vehicle),
            Formatted(TrailerNumberFormat, vehicle),
            arrivedAt,
            (1 + (vehicle % DockCount)).ToString(CultureInfo.InvariantCulture),
            Bays[vehicle / TrailerTypes.Length % Bays.Length]);

        DateTimeOffset unloadedAt = arrivedAt + UnloadingAfterArrival;
        int photos = 1 + (vehicle / (TrailerTypes.Length * Bays.Length) % PhotoVariants);
        CargoLine firstLine = shipment.CargoLines.First();
        foreach (CargoLine line in shipment.CargoLines)
        {
            int quantity = line == firstLine ? line.Quantity + quantityDelta : line.Quantity;
            script.Unload(shipment, line.UnitType, quantity, photos, unloadedAt);
        }

        if (quantityDelta > 0)
        {
            script.ResolveQuantityDelta(shipment, unloadedAt + AlertResolvedAfter);
        }

        DateTimeOffset receivedAt = unloadedAt + ReceivingAfterUnloading;
        script.FinishReceiving(shipment, receivedAt);
        if (quantityDelta < 0)
        {
            script.ResolveQuantityDelta(shipment, receivedAt + AlertResolvedAfter);
        }
    }

    // One outbound trailer per order takes whatever was actually received.
    private void LoadEverything(OrderScript script, DateTimeOffset loadedAt)
    {
        string trailerNumber = Formatted(TrailerNumberFormat, NextVehicleNumber());
        foreach (Shipment shipment in script.Order.Shipments)
        {
            foreach (CargoLine line in shipment.CargoLines)
            {
                script.Load(shipment, line.UnitType, shipment.OnStock(line.UnitType), trailerNumber, loadedAt);
            }
        }
    }

    private int NextVehicleNumber()
    {
        return FirstVehicleNumber + vehicleCount++;
    }
}
