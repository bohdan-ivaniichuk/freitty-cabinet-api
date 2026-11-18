using Freitty.Application.Orders.SubmitOrder;
using Riok.Mapperly.Abstractions;

namespace Freitty.Api.Endpoints.Orders.SubmitOrder;

[Mapper]
internal static partial class SubmitOrderRequestMapper
{
    public static partial SubmitOrderCommand ToCommand(this SubmitOrderRequest request, string orderNumber);
}
