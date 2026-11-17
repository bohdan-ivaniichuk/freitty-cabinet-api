using Freitty.Domain.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class HubConfiguration : IEntityTypeConfiguration<Hub>
{
    public void Configure(EntityTypeBuilder<Hub> builder)
    {
        builder.ToTable("hubs");
        builder.Property(hub => hub.Id).ValueGeneratedNever();
        builder.Property(hub => hub.Name).HasMaxLength(100);
        builder.Property(hub => hub.ShortName).HasMaxLength(20);
        builder.Property(hub => hub.Region).HasMaxLength(3);
        builder.Property(hub => hub.TimeZone).HasMaxLength(64);
        builder.HasIndex(hub => hub.Name).IsUnique();
    }
}
