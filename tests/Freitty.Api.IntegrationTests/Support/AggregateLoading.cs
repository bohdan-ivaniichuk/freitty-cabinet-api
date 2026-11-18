using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Support;

internal static class AggregateLoading
{
    public static Task<Order> LoadOrderAsync(this AppDbContext db, Guid orderId)
    {
        return db.Orders
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.CargoLines)
            .Include(order => order.Shipments)
                .ThenInclude(shipment => shipment.Operations)
                .ThenInclude(operation => operation.Photos)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Alerts)
            .Include(order => order.SupplyLines)
            .Include(order => order.Services)
            .AsSplitQuery()
            .SingleAsync(order => order.Id == orderId, TestContext.Current.CancellationToken);
    }
}
