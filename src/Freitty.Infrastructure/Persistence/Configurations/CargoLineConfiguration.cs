using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class CargoLineConfiguration : IEntityTypeConfiguration<CargoLine>
{
    public void Configure(EntityTypeBuilder<CargoLine> builder)
    {
        builder.ToTable("cargo_lines", table =>
        {
            table.HasEnumCheckConstraint<CargoLine, UnitType>("unit_type");
            table.HasCheckConstraint("ck_cargo_lines_quantity", "quantity > 0");
        });
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.HasIndex(line => new { line.ShipmentId, line.UnitType }).IsUnique();
    }
}
