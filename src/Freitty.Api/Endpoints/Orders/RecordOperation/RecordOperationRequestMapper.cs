using Freitty.Application.Orders.RecordOperation;
using Riok.Mapperly.Abstractions;

namespace Freitty.Api.Endpoints.Orders.RecordOperation;

[Mapper]
internal static partial class RecordOperationRequestMapper
{
    public static partial RecordOperationCommand ToCommand(this RecordOperationRequest request, string orderNumber);
}
