using Freitty.Domain.Catalog;

namespace Freitty.Domain.Orders;

public sealed class OrderService
{
    internal OrderService(Guid orderId, Guid serviceId)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        ServiceId = serviceId;
    }

    private OrderService()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Service Service { get; private set; } = null!;
}
