using Freitty.Domain.Orders;
using Riok.Mapperly.Abstractions;

namespace Freitty.Application.Orders.RecordOperation;

[Mapper]
internal static partial class RecordOperationMapper
{
    // The order, the shipment and the version select where the Operation goes; there is no chat, so it is recorded
    // without comments.
    [MapperIgnoreSource(nameof(RecordOperationCommand.Number))]
    [MapperIgnoreSource(nameof(RecordOperationCommand.ShipmentPosition))]
    [MapperIgnoreSource(nameof(RecordOperationCommand.ExpectedVersion))]
    [MapValue(nameof(NewOperation.CommentCount), 0)]
    public static partial NewOperation ToNewOperation(this RecordOperationCommand command);

    [MapperIgnoreSource(nameof(Operation.ShipmentId))]
    public static partial OperationDto ToDto(this Operation operation);

    // Three members of a large aggregate: only the target's members are required to be mapped.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial RecordedOrderDto ToRecordedOrder(this Order order);
}
