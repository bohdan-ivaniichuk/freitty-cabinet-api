using Freitty.Application.Orders.AttachPhotos;
using Riok.Mapperly.Abstractions;

namespace Freitty.Api.Endpoints.Orders.AttachPhotos;

[Mapper]
internal static partial class AttachPhotosRequestMapper
{
    public static partial AttachPhotosCommand ToCommand(
        this AttachPhotosRequest request,
        string orderNumber,
        Guid operationId);

    // The form holds the files on disk or in memory until the request ends, so each read opens them again.
    private static PhotoFile ToPhotoFile(IFormFile file)
    {
        return new PhotoFile(file.FileName, file.Length, file.OpenReadStream);
    }
}
