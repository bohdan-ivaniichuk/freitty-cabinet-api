using Freitty.Domain.Common;

namespace Freitty.Domain.Orders;

public static class OrderErrors
{
    // The codes another layer names: the application reuses one, and the API documents them.
    public const string NotDraftCode = "order.not_draft";
    public const string NotReadyToSubmitCode = "order.not_ready_to_submit";
    public const string NotActiveCode = "order.not_active";
    public const string ShipmentNotFoundCode = "shipment.not_found";
    public const string ExceedsStockCode = "operation.exceeds_stock";
    public const string OperationNotFoundCode = "operation.not_found";
    public const string PhotoLimitCode = "operation.photo_limit";

    public static DomainError NotDraft(string orderNumber, OrderStatus status)
    {
        return DomainError.Conflict(
            NotDraftCode,
            $"Order {orderNumber} is {status}; only a draft can be changed this way.");
    }

    public static DomainError NotReadyToSubmit(string orderNumber, IEnumerable<string> missingParts)
    {
        return DomainError.Conflict(
            NotReadyToSubmitCode,
            $"Order {orderNumber} cannot be submitted; it still needs {string.Join(", ", missingParts)}.");
    }

    public static DomainError NotActive(string orderNumber, OrderStatus status)
    {
        return DomainError.Conflict(
            NotActiveCode,
            $"Order {orderNumber} is {status}; the hub works only on a Ready or InProgress order.");
    }

    public static DomainError NotInProgress(string orderNumber, OrderStatus status)
    {
        return DomainError.Conflict(
            "order.not_in_progress",
            $"Order {orderNumber} is {status}; only an InProgress order can be completed.");
    }

    public static DomainError AlreadyCompleted(string orderNumber)
    {
        return DomainError.Conflict("order.already_completed", $"Order {orderNumber} is already completed.");
    }

    public static DomainError SameHub(string orderNumber)
    {
        return DomainError.Conflict(
            "order.same_hub",
            $"Order {orderNumber} cannot be transferred to its own hub.");
    }

    public static DomainError ReceivingNotFinished(string orderNumber, string shipmentLabel)
    {
        return DomainError.Conflict(
            "order.receiving_not_finished",
            $"Cannot complete {orderNumber}: receiving of {shipmentLabel} is not finished.");
    }

    public static DomainError StockRemaining(string orderNumber, string shipmentLabel)
    {
        return DomainError.Conflict(
            "order.stock_remaining",
            $"Cannot complete {orderNumber}: freight of {shipmentLabel} is still on stock.");
    }

    public static DomainError OpenAlerts(string orderNumber, string shipmentLabel)
    {
        return DomainError.Conflict(
            "order.open_alerts",
            $"Cannot complete {orderNumber}: {shipmentLabel} has an open alert.");
    }

    public static DomainError ShipmentNotFound(string orderNumber, Guid shipmentId)
    {
        return DomainError.NotFound(
            ShipmentNotFoundCode,
            $"Order {orderNumber} has no shipment {shipmentId}.");
    }

    public static DomainError DuplicateRefNumber(string orderNumber, string refNumber)
    {
        return DomainError.Conflict(
            "shipment.duplicate_ref_number",
            $"Order {orderNumber} already has a shipment with Ref N {refNumber}.");
    }

    public static DomainError CrossDockHasOne(string orderNumber)
    {
        return DomainError.Conflict(
            "shipment.cross_dock_has_one",
            $"Order {orderNumber} is a Cross-Dock and already has its one shipment.");
    }

    public static DomainError AlreadyArrived(string shipmentLabel)
    {
        return DomainError.Conflict("shipment.already_arrived", $"Shipment {shipmentLabel} has already arrived.");
    }

    public static DomainError AlreadyReceived(string shipmentLabel)
    {
        return DomainError.Conflict(
            "shipment.already_received",
            $"Receiving of shipment {shipmentLabel} is already finished.");
    }

    public static DomainError CargoLineAlreadyDeclared(string shipmentLabel, UnitType unitType)
    {
        return DomainError.Conflict(
            "cargo_line.already_declared",
            $"Shipment {shipmentLabel} already declares {unitType}.");
    }

    public static DomainError ServiceAlreadyAdded(string orderNumber, Guid serviceId)
    {
        return DomainError.Conflict(
            "service.already_added",
            $"Order {orderNumber} already includes service {serviceId}.");
    }

    public static DomainError ExceedsStock(
        string shipmentLabel,
        OperationType operationType,
        UnitType unitType,
        int quantity,
        int onStock)
    {
        return DomainError.Conflict(
            ExceedsStockCode,
            $"Cannot {Verb(operationType)} {quantity} {unitType} on {shipmentLabel}: {onStock} on stock.");
    }

    public static DomainError OperationNotFound(string orderNumber, Guid operationId)
    {
        return DomainError.NotFound(
            OperationNotFoundCode,
            $"Order {orderNumber} has no operation {operationId}.");
    }

    public static DomainError PhotoLimit(
        string shipmentLabel,
        OperationType operationType,
        int photoCount,
        int addedCount)
    {
        return DomainError.Conflict(
            PhotoLimitCode,
            $"The {operationType} of {shipmentLabel} has {photoCount} photo{(photoCount == 1 ? string.Empty : "s")}; " +
            $"{addedCount} more would exceed the " +
            $"limit of {Operation.MaxPhotoCount}.");
    }

    public static DomainError AlertNotFound(string orderNumber, Guid alertId)
    {
        return DomainError.NotFound("alert.not_found", $"Order {orderNumber} has no alert {alertId}.");
    }

    public static DomainError AlertAlreadyResolved(string shipmentLabel, AlertType alertType)
    {
        return DomainError.Conflict(
            "alert.already_resolved",
            $"The {alertType} alert of {shipmentLabel} is already resolved.");
    }

    private static string Verb(OperationType operationType)
    {
        return operationType switch
        {
            OperationType.Unloading => "unload",
            OperationType.Disposal => "dispose of",
            OperationType.Restack => "restack",
            OperationType.Loading => "load",
            _ => operationType.ToString(),
        };
    }
}
