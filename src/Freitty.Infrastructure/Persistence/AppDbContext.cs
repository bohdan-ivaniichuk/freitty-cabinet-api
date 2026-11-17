using Freitty.Domain.Billing;
using Freitty.Domain.Carriers;
using Freitty.Domain.Catalog;
using Freitty.Domain.Companies;
using Freitty.Domain.Hubs;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Infrastructure.Persistence;

// Children of Order have no DbSet: they are reached through the aggregate.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Hub> Hubs => Set<Hub>();

    public DbSet<Carrier> Carriers => Set<Carrier>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
