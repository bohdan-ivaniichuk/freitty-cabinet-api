using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class OrderServiceConfiguration : IEntityTypeConfiguration<OrderService>
{
    public void Configure(EntityTypeBuilder<OrderService> builder)
    {
        builder.ToTable("order_services");
        builder.Property(orderService => orderService.Id).ValueGeneratedNever();
        builder.HasIndex(orderService => new { orderService.OrderId, orderService.ServiceId }).IsUnique();

        builder.HasOne(orderService => orderService.Service)
            .WithMany()
            .HasForeignKey(orderService => orderService.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
