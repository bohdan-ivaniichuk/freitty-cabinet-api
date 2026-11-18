using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Support;

// The body of the request that records an operation, as a client sends it. Photos are not part of it: they are
// uploaded to the operation once it is recorded.
internal sealed record OperationRequest(
    int ShipmentPosition,
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    bool IsBillable,
    int ExpectedVersion)
{
    public static OperationRequest Unloading(Order order, int quantity, int position = 1)
    {
        return new OperationRequest(
            position,
            OperationType.Unloading,
            UnitType.Standard,
            quantity,
            OrderBuilder.TrailerNumber,
            IsBillable: true,
            order.Version);
    }

    public static OperationRequest Loading(Order order, int quantity, int position = 1)
    {
        return Unloading(order, quantity, position) with { Type = OperationType.Loading };
    }
}
