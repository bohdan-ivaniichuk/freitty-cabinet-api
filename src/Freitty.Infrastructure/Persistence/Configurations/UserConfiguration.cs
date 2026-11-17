using Freitty.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasEnumCheckConstraint<User, UserRole>("role"));
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Name).HasMaxLength(200);

        builder.HasOne(user => user.Company)
            .WithMany()
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
