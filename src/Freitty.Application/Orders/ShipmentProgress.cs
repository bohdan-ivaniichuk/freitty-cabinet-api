namespace Freitty.Application.Orders;

// What OrderProgress needs to know about one shipment.
public sealed record ShipmentProgress(bool HasArrived, bool HasOperation, bool IsReceived, bool HasStock)
{
    // Something is On stock when any Unit Type of the shipment still has freight at the hub.
    public static ShipmentProgress Of(
        bool hasArrived,
        bool hasOperation,
        bool isReceived,
        IEnumerable<ShipmentQuantities> quantities)
    {
        return new ShipmentProgress(
            hasArrived,
            hasOperation,
            isReceived,
            quantities.Any(quantity => quantity.OnStock > 0));
    }
}
