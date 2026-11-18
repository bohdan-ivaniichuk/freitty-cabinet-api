using Freitty.Domain.Common;

namespace Freitty.Application.Orders;

// The refusals about photos the aggregate cannot make: it does not know what other orders hold, and a photo is looked
// up by the request, not changed.
public static class PhotoRequestErrors
{
    public const string PhotoNotFoundCode = "photo.not_found";
    public const string UploadLimitCode = "photo.upload_limit";

    public static DomainError PhotoNotFound(string orderNumber, Guid photoId)
    {
        return DomainError.NotFound(PhotoNotFoundCode, $"Order {orderNumber} has no photo {photoId}.");
    }

    public static DomainError UploadLimit(int maxUploadedPhotoCount)
    {
        return DomainError.Conflict(
            UploadLimitCode,
            $"The demo already holds the most uploaded photos it keeps, {maxUploadedPhotoCount}. They are deleted " +
            "when the sample data is next rebuilt; try again after that.");
    }
}
