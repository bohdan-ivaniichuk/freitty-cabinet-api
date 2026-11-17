using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;

namespace Freitty.Infrastructure.Persistence.Seed;

internal sealed class ReferenceData
{
    public const string HubTimeZone = "America/Toronto";

    // Drivers and floor leads are not Users: the model keeps them as plain names.
    public const string OwnDriver = "User 5";
    private const string MarkhamFloorLead = "User 6";
    private const string TorontoFloorLead = "User 7";

    private readonly Dictionary<string, SupplyItem> supplyItemsBySku;

    public ReferenceData()
    {
        Company = new Company("R-way Transport", 4250.00m);
        User1 = new User(Company.Id, "User 1", UserRole.Admin);
        User2 = new User(Company.Id, "User 2", UserRole.Dispatcher);
        User3 = new User(Company.Id, "User 3", UserRole.Dispatcher);
        User4 = new User(Company.Id, "User 4", UserRole.Dispatcher);
        Markham = new Hub("Markham", "Mark", "ON", HubTimeZone);
        Toronto = new Hub("Toronto", "Tor", "ON", HubTimeZone);
        Schneider = new Carrier("Schneider", "+1 800 555 0142");
        TForce = new Carrier("TForce", "+1 800 555 0177");
        RWayTransport = new Carrier("R-way Transport Inc.", "+1 647 555 0199");
        Transload = new Service("Transload");
        RestockAndRework = new Service("Restock & Rework");
        supplyItemsBySku = CreateSupplyItems().ToDictionary(item => item.Sku);
    }

    public Company Company { get; }

    public User User1 { get; }

    public User User2 { get; }

    public User User3 { get; }

    public User User4 { get; }

    public IReadOnlyList<User> Users => [User1, User2, User3, User4];

    public Hub Markham { get; }

    public Hub Toronto { get; }

    public Carrier Schneider { get; }

    public Carrier TForce { get; }

    public Carrier RWayTransport { get; }

    public Service Transload { get; }

    public Service RestockAndRework { get; }

    public SupplyItem SupplyItem(string sku)
    {
        return supplyItemsBySku[sku];
    }

    public string FloorLeadOf(Hub hub)
    {
        return hub == Markham ? MarkhamFloorLead : TorontoFloorLead;
    }

    public IEnumerable<object> Entities()
    {
        return
        [
            Company,
            .. Users,
            Markham,
            Toronto,
            Schneider,
            TForce,
            RWayTransport,
            Transload,
            RestockAndRework,
            .. supplyItemsBySku.Values,
        ];
    }

    private static SupplyItem[] CreateSupplyItems()
    {
        return
        [
            new("Straps 12", "Securement", 18.50m),
            new("Straps 24", "Securement", 32.00m),
            new("Ratchet strap 2in", "Securement", 14.75m),
            new("Load bar", "Securement", 42.00m),
            new("Corners 50", "Edge protect", 12.00m),
            new("Corners 100", "Edge protect", 21.50m),
            new("Edge board 48in", "Edge protect", 9.25m),
            new("Shrink wrap 120g", "Wrap", 24.00m),
            new("Shrink wrap 80g", "Wrap", 17.50m),
            new("Stretch film hand roll", "Wrap", 11.00m),
            new("Pallet cover", "Wrap", 6.50m),
            new("Pallet 48x40", "Pallets", 16.00m),
            new("Pallet XL", "Pallets", 27.00m),
            new("Air bag", "Dunnage", 8.75m),
            new("Slip sheet", "Dunnage", 3.20m),
            new("Label roll 500", "Labels", 13.40m),
        ];
    }
}
