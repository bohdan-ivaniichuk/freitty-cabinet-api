using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments", ConfigureChecks);
        builder.Property(shipment => shipment.Id).ValueGeneratedNever();
        builder.Property(shipment => shipment.RefNumber).HasMaxLength(50);
        builder.Property(shipment => shipment.WarehouseNote).HasMaxLength(2000);
        builder.Property(shipment => shipment.TruckNumber).HasMaxLength(20);
        builder.Property(shipment => shipment.TrailerNumber).HasMaxLength(20);
        builder.Property(shipment => shipment.Dock).HasMaxLength(20);
        builder.Property(shipment => shipment.Bay).HasMaxLength(20);
        builder.Property(shipment => shipment.FloorLead).HasMaxLength(200);

        builder.HasIndex(shipment => new { shipment.OrderId, shipment.Position }).IsUnique();
        builder.HasIndex(shipment => new { shipment.OrderId, shipment.RefNumber }).IsUnique();

        ConfigureChildren(builder);
    }

    private static void ConfigureChecks(TableBuilder<Shipment> table)
    {
        table.HasEnumCheckConstraint<Shipment, TrailerType>("trailer_type");
        table.HasCheckConstraint("ck_shipments_position", "position >= 1");
        table.HasCheckConstraint(
            "ck_shipments_arrival_complete",
            "num_nulls(trailer_type, truck_number, trailer_number, arrived_at) IN (0, 4)");
        table.HasCheckConstraint(
            "ck_shipments_dock_complete",
            "num_nulls(dock, bay, floor_lead, dock_assigned_at) IN (0, 4)");
    }

    // Cargo Lines only describe a shipment and follow it. The ledger is protected: a shipment with Operations
    // or Alerts cannot be deleted, and so neither can its order.
    // The foreign keys are named explicitly: Shipment has no DbSet, so the naming convention names them after the
    // entity ("shipment") before ToTable renames its table.
    private static void ConfigureChildren(EntityTypeBuilder<Shipment> builder)
    {
        builder.HasMany(shipment => shipment.CargoLines)
            .WithOne()
            .HasForeignKey(line => line.ShipmentId)
            .HasConstraintName("fk_cargo_lines_shipments_shipment_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(shipment => shipment.Operations)
            .WithOne()
            .HasForeignKey(operation => operation.ShipmentId)
            .HasConstraintName("fk_operations_shipments_shipment_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(shipment => shipment.Alerts)
            .WithOne()
            .HasForeignKey(alert => alert.ShipmentId)
            .HasConstraintName("fk_alerts_shipments_shipment_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(shipment => shipment.CargoLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(shipment => shipment.Operations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(shipment => shipment.Alerts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
