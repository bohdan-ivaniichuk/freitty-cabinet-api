using Freitty.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");
        builder.Property(service => service.Id).ValueGeneratedNever();
        builder.Property(service => service.Name).HasMaxLength(100);
        builder.HasIndex(service => service.Name).IsUnique();
    }
}
