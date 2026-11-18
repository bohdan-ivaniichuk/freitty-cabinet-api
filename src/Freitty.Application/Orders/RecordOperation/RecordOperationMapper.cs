using Freitty.Domain.Orders;
using Riok.Mapperly.Abstractions;

namespace Freitty.Application.Orders.RecordOperation;

[Mapper]
internal static partial class RecordOperationMapper
{
    // The order, the shipment and the version select where the Operation goes. There is no chat, so it is recorded
    // without comments, and its photos are attached by their own request once it exists.
    [MapperIgnoreSource(nameof(RecordOperationCommand.OrderNumber))]
    [MapperIgnoreSource(nameof(RecordOperationCommand.ShipmentPosition))]
    [MapperIgnoreSource(nameof(RecordOperationCommand.ExpectedVersion))]
    [MapValue(nameof(NewOperation.Photos), Use = nameof(NoPhotos))]
    [MapValue(nameof(NewOperation.CommentCount), 0)]
    public static partial NewOperation ToNewOperation(this RecordOperationCommand command);

    [MapperIgnoreSource(nameof(Operation.ShipmentId))]
    [MapperIgnoreSource(nameof(Operation.Photos))]
    public static partial OperationDto ToDto(this Operation operation);

    // Three members of a large aggregate: only the target's members are required to be mapped.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial RecordedOrderDto ToRecordedOrder(this Order order);

    private static IReadOnlyList<NewPhoto> NoPhotos()
    {
        return [];
    }
}
