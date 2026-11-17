using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class SupplyLineConfiguration : IEntityTypeConfiguration<SupplyLine>
{
    public void Configure(EntityTypeBuilder<SupplyLine> builder)
    {
        builder.ToTable("supply_lines", table =>
        {
            table.HasCheckConstraint("ck_supply_lines_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_supply_lines_unit_price", "unit_price >= 0");
        });
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.UnitPrice).IsMoney();

        builder.HasOne(line => line.SupplyItem)
            .WithMany()
            .HasForeignKey(line => line.SupplyItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
