using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Common;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Persistence;

// Test classes run in parallel on one database: every test creates its own reference data, and every name,
// SKU and number is unique.
internal sealed record TestData(
    Company Company,
    User User,
    Hub Hub,
    Hub OtherHub,
    Carrier Carrier,
    Service Service,
    Service OtherService,
    SupplyItem SupplyItem)
{
    private const int NumberLength = 20;
    private const int DeclaredQuantity = 10;

    public static readonly DateTimeOffset Now = new(2026, 3, 2, 13, 0, 0, TimeSpan.Zero);

    public static TestData Create()
    {
        var company = new Company(Unique("Company "), 1250.50m);
        return new TestData(
            company,
            new User(company.Id, "User 1", UserRole.Admin),
            new Hub(Unique("Markham "), "Mark", "ON", "America/Toronto"),
            new Hub(Unique("Toronto "), "Tor", "ON", "America/Toronto"),
            new Carrier(Unique("Carrier "), "+1 416 555 0100"),
            new Service(Unique("Transload ")),
            new Service(Unique("Restock ")),
            new SupplyItem(Unique("STRAP-"), "Straps", 12.50m));
    }

    public static string Unique(string prefix, int maxLength = NumberLength)
    {
        string value = prefix + Guid.NewGuid().ToString("N");
        return value[..Math.Min(maxLength, value.Length)];
    }

    public object[] Entities()
    {
        return [Company, User, Hub, OtherHub, Carrier, Service, OtherService, SupplyItem];
    }

    // An Unloading of exactly what DraftWithCargo declares, so it raises no QuantityDelta alert.
    public static NewOperation DeclaredUnloading(int photoCount)
    {
        return new NewOperation(
            OperationType.Unloading,
            UnitType.Standard,
            DeclaredQuantity,
            TrailerNumber: "TL-1",
            IsBillable: true,
            PhotoCount: photoCount,
            CommentCount: 0);
    }

    public Order Draft(string? draftNumber = null)
    {
        return Order.StartDraft(
            draftNumber ?? Unique("DRAFT-"),
            OrderType.CrossDock,
            CrossDockKind.Storage,
            User,
            Hub.Id,
            Now);
    }

    public Order DraftWithCargo()
    {
        Order order = Draft();
        Shipment shipment = order.AddShipment(Unique("REF-"), Now).Value;
        Succeed(order.DeclareCargo(shipment.Id, UnitType.Standard, DeclaredQuantity, Now));
        return order;
    }

    public Order Ready(string? orderNumber = null)
    {
        Order order = DraftWithCargo();
        Succeed(order.Schedule(Now.AddDays(1), Now));
        Succeed(order.SetDestination("Montreal", "QC", Now));
        Succeed(order.ArrangeCarrier(Carrier.Id, Now));
        Succeed(order.Submit(orderNumber ?? Unique("FR"), Now));
        return order;
    }

    public Order InProgress()
    {
        Order order = Ready();
        Succeed(order.RecordArrival(order.Shipments.Single().Id, TrailerType.DryVan53, "TR-1", "TL-1", Now));
        return order;
    }

    public static void Succeed(Result result)
    {
        Assert.True(result.IsSuccess, result.Error?.Message);
    }
}
