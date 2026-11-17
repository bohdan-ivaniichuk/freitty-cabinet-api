using Freitty.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", table =>
        {
            table.HasCheckConstraint("ck_invoices_amount", "amount >= 0");
            table.HasCheckConstraint("ck_invoices_paid_after_issued", "paid_at >= issued_at");
        });
        builder.Property(invoice => invoice.Id).ValueGeneratedNever();
        builder.Property(invoice => invoice.Number).HasMaxLength(20);
        builder.Property(invoice => invoice.Amount).IsMoney();
        builder.HasIndex(invoice => invoice.Number).IsUnique();

        builder.HasOne(invoice => invoice.Order)
            .WithMany()
            .HasForeignKey(invoice => invoice.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
