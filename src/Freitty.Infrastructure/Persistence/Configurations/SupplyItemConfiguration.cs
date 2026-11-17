using Freitty.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class SupplyItemConfiguration : IEntityTypeConfiguration<SupplyItem>
{
    public void Configure(EntityTypeBuilder<SupplyItem> builder)
    {
        builder.ToTable(
            "supply_items",
            table => table.HasCheckConstraint("ck_supply_items_unit_price", "unit_price >= 0"));
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Sku).HasMaxLength(100);
        builder.Property(item => item.Category).HasMaxLength(100);
        builder.Property(item => item.UnitPrice).IsMoney();
        builder.HasIndex(item => item.Sku).IsUnique();
    }
}
