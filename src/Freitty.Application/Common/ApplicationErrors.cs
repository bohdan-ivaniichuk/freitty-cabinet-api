using Freitty.Domain.Common;

namespace Freitty.Application.Common;

// The refusals the aggregate cannot make itself: an order that was never loaded cannot refuse anything, the
// position is how a client names a shipment, and the version is a matter between the client and the database.
public static class ApplicationErrors
{
    // Both ways to lose a write to another one: the client saw an older version, or another write landed first.
    private const string VersionConflictCode = "order.version_conflict";

    public static DomainError OrderNotFound(string orderNumber)
    {
        return DomainError.NotFound("order.not_found", $"Order {orderNumber} not found.");
    }

    public static DomainError ShipmentNotFound(string orderNumber, int position)
    {
        return DomainError.NotFound(
            "shipment.not_found",
            $"Order {orderNumber} has no shipment at position {position}.");
    }

    public static DomainError VersionConflict(string orderNumber, int version, int expectedVersion)
    {
        return DomainError.Conflict(
            VersionConflictCode,
            $"Order {orderNumber} was changed since you loaded it (version {version}, expected {expectedVersion}). " +
            "Reload and try again.");
    }

    public static DomainError ChangedConcurrently(string orderNumber)
    {
        return DomainError.Conflict(
            VersionConflictCode,
            $"Order {orderNumber} was changed while this request was being processed. Reload and try again.");
    }
}
