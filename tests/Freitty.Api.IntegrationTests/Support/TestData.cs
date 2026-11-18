using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;

namespace Freitty.Api.IntegrationTests.Support;

// The reference data a test's orders point to. Test classes run in parallel on one database: every test creates its
// own, and every name, SKU and number is unique.
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
}
