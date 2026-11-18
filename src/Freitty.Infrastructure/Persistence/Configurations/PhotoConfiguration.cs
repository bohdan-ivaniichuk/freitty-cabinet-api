using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("photos", table => table.HasCheckConstraint("ck_photos_size_bytes", "size_bytes > 0"));
        builder.Property(photo => photo.Id).ValueGeneratedNever();
        builder.Property(photo => photo.StorageKey).HasMaxLength(200);
        builder.Property(photo => photo.ContentType).HasMaxLength(50);
        builder.HasIndex(photo => photo.OperationId);
    }
}
