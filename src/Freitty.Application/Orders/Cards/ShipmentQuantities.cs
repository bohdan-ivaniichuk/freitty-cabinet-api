using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.Cards;

// The read side's quantities of one shipment and Unit Type, by the definitions of CONTEXT.md. The computed members
// of the aggregate need the loaded ledger; a read has only the sums, so the same arithmetic lives here once.
public sealed record ShipmentQuantities(UnitTypeSums Sums, bool IsReceived)
{
    public UnitType UnitType => Sums.UnitType;

    public int Declared => Sums.Declared;

    public int Actual => Sums.Unloaded;

    public int OnStock => Sums.Unloaded - Sums.Disposed - Sums.Loaded;

    // An excess is known as soon as it is unloaded; a shortage only once the shipment is received, because the
    // rest may still be on the next truck.
    public int? KnownDelta => Actual > Declared || IsReceived ? Actual - Declared : null;

    public bool HasQuantityDelta => KnownDelta is not (null or 0);
}
