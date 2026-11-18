using Freitty.Domain.Billing;
using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Abstractions;

// Children of Order have no set: a read reaches them through db.Orders and SelectMany.
public interface IAppDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<Company> Companies { get; }

    DbSet<User> Users { get; }

    DbSet<Hub> Hubs { get; }

    DbSet<Carrier> Carriers { get; }

    DbSet<Service> Services { get; }

    DbSet<SupplyItem> SupplyItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    // False when another writer changed the same order first; any other failure is thrown. After false the context
    // still tracks the refused changes and must not be saved again.
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
