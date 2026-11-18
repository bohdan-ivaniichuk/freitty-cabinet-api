namespace Freitty.Application.Orders;

// How a shipment is named to the client: <number>-<position> for every shipment, the single one of a Cross-Dock
// included. The aggregate names its shipments the same way in its refusals.
public static class ShipmentLabel
{
    public static string Of(string orderNumber, int position)
    {
        return $"{orderNumber}-{position}";
    }
}
