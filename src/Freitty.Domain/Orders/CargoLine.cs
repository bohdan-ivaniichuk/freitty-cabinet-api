namespace Freitty.Domain.Orders;

public sealed class CargoLine
{
    internal CargoLine(Guid shipmentId, UnitType unitType, int quantity)
    {
        Id = Guid.CreateVersion7();
        ShipmentId = shipmentId;
        UnitType = unitType;
        Quantity = quantity;
    }

    private CargoLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid ShipmentId { get; private set; }

    public UnitType UnitType { get; private set; }

    public int Quantity { get; private set; }
}
