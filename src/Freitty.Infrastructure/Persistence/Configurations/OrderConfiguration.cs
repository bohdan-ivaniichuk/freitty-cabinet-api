using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    private const int Draft = (int)OrderStatus.Draft;
    private const int InProgress = (int)OrderStatus.InProgress;
    private const int Completed = (int)OrderStatus.Completed;
    private const string OrderNumberTrigramIndex = "ix_orders_number_trgm";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", ConfigureChecks);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.Number).HasMaxLength(20);
        builder.Property(order => order.DestinationCity).HasMaxLength(100);
        builder.Property(order => order.DestinationRegion).HasMaxLength(3);
        builder.Property(order => order.DriverName).HasMaxLength(200);

        // Every change of the aggregate increments it, so a write that read an older version updates no row.
        builder.Property(order => order.Version).IsConcurrencyToken();

        // Draft numbers are the company's own; official numbers are unique across the platform.
        builder.HasIndex(order => new { order.CompanyId, order.Number }).IsUnique();
        builder.HasIndex(order => order.Number).IsUnique().HasFilter($"status <> {Draft}");

        // The search's ILIKE '%…%', and the default list: newest Scheduled date first, then id. The trigram index
        // needs the named overload, since the unnamed one would reconfigure the unique index on the same column,
        // and a database name of its own, since the naming convention would number it.
        builder.HasIndex(order => order.Number, OrderNumberTrigramIndex)
            .HasDatabaseName(OrderNumberTrigramIndex)
            .IsTrigramIndex();
        builder.HasIndex(order => new { order.CompanyId, order.ScheduledAt, order.Id })
            .IsDescending(false, true, true);

        ConfigureReferences(builder);
        ConfigureChildren(builder);
    }

    private static void ConfigureChecks(TableBuilder<Order> table)
    {
        table.HasEnumCheckConstraint<Order, OrderType>("type");
        table.HasEnumCheckConstraint<Order, CrossDockKind>("cross_dock_kind");
        table.HasEnumCheckConstraint<Order, OrderStatus>("status");
        table.HasEnumCheckConstraint<Order, TransportKind>("transport_kind");

        table.HasCheckConstraint(
            "ck_orders_cross_dock_kind_matches_type",
            $"(type = {(int)OrderType.CrossDock}) = (cross_dock_kind IS NOT NULL)");

        // IS NOT DISTINCT FROM, not =: transport_kind is nullable, and a CHECK passes on NULL, so the plain form
        // would accept a carrier on an order with no transport kind.
        table.HasCheckConstraint(
            "ck_orders_carrier_matches_transport",
            $"(transport_kind IS NOT DISTINCT FROM {(int)TransportKind.Carrier}) = (carrier_id IS NOT NULL)");
        table.HasCheckConstraint(
            "ck_orders_driver_matches_transport",
            $"(transport_kind IS NOT DISTINCT FROM {(int)TransportKind.OwnDriver}) = (driver_name IS NOT NULL)");

        table.HasCheckConstraint("ck_orders_transfer_hub_differs", "transfer_hub_id <> hub_id");
        table.HasCheckConstraint(
            "ck_orders_destination_complete",
            "(destination_city IS NULL) = (destination_region IS NULL)");

        table.HasCheckConstraint(
            "ck_orders_submitted_at_matches_status",
            $"(status <> {Draft}) = (submitted_at IS NOT NULL)");
        table.HasCheckConstraint(
            "ck_orders_started_at_matches_status",
            $"(status IN ({InProgress}, {Completed})) = (started_at IS NOT NULL)");
        table.HasCheckConstraint(
            "ck_orders_completed_at_matches_status",
            $"(status = {Completed}) = (completed_at IS NOT NULL)");
        table.HasCheckConstraint(
            "ck_orders_milestones_in_order",
            "created_at <= submitted_at AND submitted_at <= started_at AND started_at <= completed_at");

        table.HasCheckConstraint(
            "ck_orders_submitted_has_details",
            $"status = {Draft} OR " +
            "(scheduled_at IS NOT NULL AND destination_city IS NOT NULL AND transport_kind IS NOT NULL)");
        table.HasCheckConstraint("ck_orders_version", "version >= 1");
    }

    private static void ConfigureReferences(EntityTypeBuilder<Order> builder)
    {
        builder.HasOne(order => order.Company)
            .WithMany()
            .HasForeignKey(order => order.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.CreatedBy)
            .WithMany()
            .HasForeignKey(order => order.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Hub)
            .WithMany()
            .HasForeignKey(order => order.HubId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.TransferHub)
            .WithMany()
            .HasForeignKey(order => order.TransferHubId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Carrier)
            .WithMany()
            .HasForeignKey(order => order.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // Rows that only describe an order follow it; the ledger under its shipments does not (see Shipment).
    private static void ConfigureChildren(EntityTypeBuilder<Order> builder)
    {
        builder.HasMany(order => order.Shipments)
            .WithOne()
            .HasForeignKey(shipment => shipment.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(order => order.SupplyLines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(order => order.Services)
            .WithOne()
            .HasForeignKey(service => service.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Shipments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(order => order.SupplyLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(order => order.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
