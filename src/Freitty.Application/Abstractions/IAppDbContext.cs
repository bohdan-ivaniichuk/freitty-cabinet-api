using Freitty.Domain.Billing;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Abstractions;

// What the handlers read and write. Children of Order have no set: a read reaches them through db.Orders and
// SelectMany. Saving goes through TrySaveChangesAsync alone, so no handler can turn a lost race into an exception.
public interface IAppDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<User> Users { get; }

    DbSet<Hub> Hubs { get; }

    // False when another writer changed the same order first; any other failure is thrown. After false the context
    // still tracks the refused changes and must not be saved again.
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
