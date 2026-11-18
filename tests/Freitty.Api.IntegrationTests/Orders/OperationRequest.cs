using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// The body of POST /api/v1/orders/{number}/operations, as a client sends it.
internal sealed record OperationRequest(
    int ShipmentPosition,
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    bool IsBillable,
    int PhotoCount,
    int ExpectedVersion)
{
    public static string UrlOf(string number)
    {
        return $"/api/v1/orders/{number}/operations";
    }

    public static OperationRequest Unloading(Order order, int quantity, int photos = 1, int position = 1)
    {
        return new OperationRequest(
            position,
            OperationType.Unloading,
            UnitType.Standard,
            quantity,
            OrderBuilder.TrailerNumber,
            IsBillable: true,
            photos,
            order.Version);
    }

    public static OperationRequest Loading(Order order, int quantity, int position = 1)
    {
        return Unloading(order, quantity, photos: 0, position) with { Type = OperationType.Loading };
    }
}
