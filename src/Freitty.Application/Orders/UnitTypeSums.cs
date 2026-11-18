using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// The ledger of one shipment and Unit Type, summed in SQL: one GROUP BY over its Cargo Lines and Operations.
internal sealed record UnitTypeSums(
    Guid ShipmentId,
    UnitType UnitType,
    int Declared,
    int Unloaded,
    int Disposed,
    int Loaded)
{
    public static IQueryable<UnitTypeSums> Of(IQueryable<Shipment> shipments)
    {
        var declared = shipments
            .SelectMany(shipment => shipment.CargoLines)
            .Select(line => new
            {
                line.ShipmentId,
                line.UnitType,
                Declared = line.Quantity,
                Unloaded = 0,
                Disposed = 0,
                Loaded = 0,
            });
        var operated = shipments
            .SelectMany(shipment => shipment.Operations)
            .Select(operation => new
            {
                operation.ShipmentId,
                operation.UnitType,
                Declared = 0,
                Unloaded = operation.Type == OperationType.Unloading ? operation.Quantity : 0,
                Disposed = operation.Type == OperationType.Disposal ? operation.Quantity : 0,
                Loaded = operation.Type == OperationType.Loading ? operation.Quantity : 0,
            });

        return declared
            .Concat(operated)
            .GroupBy(row => new { row.ShipmentId, row.UnitType })
            .Select(group => new UnitTypeSums(
                group.Key.ShipmentId,
                group.Key.UnitType,
                group.Sum(row => row.Declared),
                group.Sum(row => row.Unloaded),
                group.Sum(row => row.Disposed),
                group.Sum(row => row.Loaded)));
    }
}
