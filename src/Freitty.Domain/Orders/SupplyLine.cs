using Freitty.Domain.Catalog;

namespace Freitty.Domain.Orders;

public sealed class SupplyLine
{
    // The price is copied: a line keeps the price the item had when it was added.
    internal SupplyLine(Guid orderId, SupplyItem supplyItem, int quantity)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        SupplyItemId = supplyItem.Id;
        Quantity = quantity;
        UnitPrice = supplyItem.UnitPrice;
    }

    private SupplyLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid SupplyItemId { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public SupplyItem SupplyItem { get; private set; } = null!;

    public decimal LineTotal => Quantity * UnitPrice;
}
