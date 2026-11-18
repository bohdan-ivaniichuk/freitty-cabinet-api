using Freitty.Application.Abstractions;
using Freitty.Domain.Billing;
using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Freitty.Infrastructure.Persistence;

// Children of Order have no DbSet: they are reached through the aggregate.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Hub> Hubs => Set<Hub>();

    public DbSet<Carrier> Carriers => Set<Carrier>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();

    // A writer that loses the race to the same order meets one of two things, depending on which statement comes
    // first: the update of the order row finds another version, or the insert of an Alert finds the same open
    // Alert, which the partial unique index allows once. EF Core sends the inserts before the update.
    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException exception) when (IsDuplicateOpenAlert(exception))
        {
            return false;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private static bool IsDuplicateOpenAlert(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AlertConfiguration.OpenAlertIndex,
        };
    }
}
