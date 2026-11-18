using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.AttachPhotos;

// The photos just added, the operation's count after them, the order's new version and the alerts the upload closed.
public sealed record AttachedPhotosDto(
    int Version,
    int PhotoCount,
    IReadOnlyList<AttachedPhotoDto> Photos,
    IReadOnlyList<AlertType> ResolvedAlertTypes);
