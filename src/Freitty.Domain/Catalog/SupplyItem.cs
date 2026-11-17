using Freitty.Domain.Common;

namespace Freitty.Domain.Catalog;

public sealed class SupplyItem
{
    public SupplyItem(string sku, string category, decimal unitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        Id = Guid.CreateVersion7();
        Sku = Guard.Text(sku);
        Category = Guard.Text(category);
        UnitPrice = unitPrice;
    }

    private SupplyItem()
    {
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = null!;

    public string Category { get; private set; } = null!;

    public decimal UnitPrice { get; private set; }
}
