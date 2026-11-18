namespace Freitty.Application.Orders.RecordOperation;

// Only what identifies the result: the page reloads the order after a write.
public sealed record RecordedOperationDto(OperationDto Operation, RecordedOrderDto Order);
