using Freitty.Domain.Carriers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class CarrierConfiguration : IEntityTypeConfiguration<Carrier>
{
    public void Configure(EntityTypeBuilder<Carrier> builder)
    {
        builder.ToTable("carriers");
        builder.Property(carrier => carrier.Id).ValueGeneratedNever();
        builder.Property(carrier => carrier.Name).HasMaxLength(200);
        builder.Property(carrier => carrier.Phone).HasMaxLength(32);
        builder.HasIndex(carrier => carrier.Name).IsUnique();
    }
}
