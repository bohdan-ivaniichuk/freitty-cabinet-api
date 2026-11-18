using Freitty.Domain.Orders;
using Riok.Mapperly.Abstractions;

namespace Freitty.Application.Orders.AttachPhotos;

[Mapper]
internal static partial class AttachPhotosMapper
{
    // The count is the operation's; the version is the order's, which the attachment does not carry.
    [MapProperty(
        [nameof(PhotoAttachment.Operation), nameof(Operation.PhotoCount)],
        nameof(AttachedPhotosDto.PhotoCount))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial AttachedPhotosDto ToDto(this PhotoAttachment attachment, int version);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private static partial AttachedPhotoDto ToDto(Photo photo);
}
