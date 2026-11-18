using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.GetPhoto;

// The row says where the bytes are. A row whose bytes are gone from storage is not found either: a client can act on
// that, and on a 500 it cannot.
public sealed class GetPhotoQueryHandler(IAppDbContext db, ICurrentUser currentUser, IPhotoStorage photoStorage)
    : ICommandHandler<GetPhotoQuery, Result<PhotoContentDto>>
{
    public async Task<Result<PhotoContentDto>> ExecuteAsync(GetPhotoQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        StoredPhoto? photo = await db.Orders
            .AsNoTracking()
            .WithNumber(user.CompanyId, query.OrderNumber)
            .SelectMany(order => order.Shipments)
            .SelectMany(shipment => shipment.Operations)
            .SelectMany(operation => operation.Photos)
            .Where(candidate => candidate.Id == query.PhotoId)
            .Select(candidate => new StoredPhoto(candidate.StorageKey, candidate.ContentType, candidate.SizeBytes))
            .FirstOrDefaultAsync(ct);
        if (photo is null)
        {
            return PhotoRequestErrors.PhotoNotFound(query.OrderNumber, query.PhotoId);
        }

        Stream? content = await photoStorage.OpenAsync(photo.StorageKey, ct);
        if (content is null)
        {
            return PhotoRequestErrors.PhotoNotFound(query.OrderNumber, query.PhotoId);
        }

        return new PhotoContentDto(content, photo.ContentType, photo.SizeBytes);
    }

    private sealed record StoredPhoto(string StorageKey, string ContentType, long SizeBytes);
}
