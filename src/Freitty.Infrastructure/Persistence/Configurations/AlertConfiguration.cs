using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts", table =>
        {
            table.HasEnumCheckConstraint<Alert, AlertType>("type");
            table.HasCheckConstraint("ck_alerts_resolved_after_raised", "resolved_at >= raised_at");
        });
        builder.Property(alert => alert.Id).ValueGeneratedNever();

        // One open alert per shipment and type. EF Core counts this partial index as covering the foreign key
        // and creates none, yet it does not serve a lookup of all alerts of a shipment — hence the plain one.
        builder.HasIndex(alert => new { alert.ShipmentId, alert.Type }).IsUnique().HasFilter("resolved_at IS NULL");
        builder.HasIndex(alert => alert.ShipmentId);
    }
}
