using Freitty.Domain.Orders;
using Riok.Mapperly.Abstractions;

namespace Freitty.Application.Orders.SubmitOrder;

[Mapper]
internal static partial class SubmitOrderMapper
{
    // Three members of a large aggregate: only the target's members are required to be mapped.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial SubmittedOrderDto ToSubmittedOrder(this Order order);
}
