using Freitty.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.Property(company => company.Id).ValueGeneratedNever();
        builder.Property(company => company.Name).HasMaxLength(200);
        builder.Property(company => company.Balance).IsMoney();
    }
}
