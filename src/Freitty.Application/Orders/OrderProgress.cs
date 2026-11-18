using Freitty.Application.Orders.Cards;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// The stage of the freight and, from it, the card's "Next" line. Each takes the first rule that applies.
internal static class OrderProgress
{
    public static OrderStage StageOf(OrderStatus status, IReadOnlyCollection<ShipmentProgress> shipments)
    {
        return status switch
        {
            OrderStatus.Draft => OrderStage.Draft,
            OrderStatus.Completed => OrderStage.Completed,
            _ when shipments.All(IsUntouched) => OrderStage.AwaitingTruck,
            _ when shipments.Any(shipment => !shipment.IsReceived) => OrderStage.Unloading,
            _ when shipments.Any(shipment => shipment.HasStock) => OrderStage.Loading,
            _ => OrderStage.AwaitingCompletion,
        };
    }

    // An open alert needs the client before anything the hub does; a completed order is left with its payment.
    public static NextStepDto NextStepOf(
        OrderStage stage,
        IReadOnlyCollection<AlertType> openAlertTypes,
        InvoiceSummary? firstUnpaidInvoice,
        InvoiceSummary? lastInvoice)
    {
        return stage switch
        {
            OrderStage.Draft => Step(NextStepCode.ContinueEditing),
            _ when openAlertTypes.Contains(AlertType.MissingPhoto) => Step(NextStepCode.UploadPhoto),
            _ when openAlertTypes.Contains(AlertType.QuantityDelta) => Step(NextStepCode.ReviewQuantityDelta),
            OrderStage.AwaitingTruck => Step(NextStepCode.AwaitingTruck),
            OrderStage.Unloading => Step(NextStepCode.Unloading),
            OrderStage.Loading => Step(NextStepCode.Loading),
            OrderStage.AwaitingCompletion => Step(NextStepCode.AwaitingCompletion),
            OrderStage.Completed when firstUnpaidInvoice is not null =>
                Step(NextStepCode.AwaitingPayment, firstUnpaidInvoice),
            OrderStage.Completed when lastInvoice is not null => Step(NextStepCode.Paid, lastInvoice),
            OrderStage.Completed => Step(NextStepCode.Closed),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "The order stage is not known."),
        };
    }

    private static bool IsUntouched(ShipmentProgress shipment)
    {
        return !shipment.HasArrived && !shipment.HasOperation && !shipment.IsReceived;
    }

    private static NextStepDto Step(NextStepCode code, InvoiceSummary? invoice = null)
    {
        return new NextStepDto(code, invoice?.Number, invoice?.Amount);
    }
}
