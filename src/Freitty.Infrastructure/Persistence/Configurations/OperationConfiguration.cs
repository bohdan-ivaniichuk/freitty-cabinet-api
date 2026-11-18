using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    private const int Unloading = (int)OperationType.Unloading;
    private const int Loading = (int)OperationType.Loading;

    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operations", table =>
        {
            table.HasEnumCheckConstraint<Operation, OperationType>("type");
            table.HasEnumCheckConstraint<Operation, UnitType>("unit_type");
            table.HasCheckConstraint("ck_operations_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_operations_photo_count", "photo_count >= 0");
            table.HasCheckConstraint("ck_operations_comment_count", "comment_count >= 0");

            // Unloading and Loading move freight between a trailer and the dock; the others have no trailer.
            table.HasCheckConstraint(
                "ck_operations_trailer_number_matches_type",
                $"(type IN ({Unloading}, {Loading})) = (trailer_number IS NOT NULL)");
        });
        builder.Property(operation => operation.Id).ValueGeneratedNever();
        builder.Property(operation => operation.TrailerNumber).HasMaxLength(Operation.MaxTrailerNumberLength);

        // A photo only documents its operation and follows it. The foreign key is named explicitly, as those of the
        // shipment's children are: Operation has no DbSet.
        builder.HasMany(operation => operation.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.OperationId)
            .HasConstraintName("fk_photos_operations_operation_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(operation => operation.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
