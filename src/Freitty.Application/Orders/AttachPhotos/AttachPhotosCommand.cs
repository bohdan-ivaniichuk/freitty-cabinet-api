using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.AttachPhotos;

// The order is named by its number and the operation by its id, which the detail lists. Version is the version of the
// order the caller saw, with the rule of recording an operation: a stale page and a retry are refused instead of
// attaching the same photos twice.
public sealed record AttachPhotosCommand(
    string OrderNumber,
    Guid OperationId,
    int Version,
    IReadOnlyList<PhotoFile> Files)
    : ICommand<Result<AttachedPhotosDto>>;
