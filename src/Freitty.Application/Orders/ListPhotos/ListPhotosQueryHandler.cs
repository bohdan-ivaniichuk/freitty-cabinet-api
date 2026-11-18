using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.ListPhotos;

// In the order of the page: shipment by shipment, each operation in the order of the ledger, its photos as they were
// uploaded. An order without photos is an empty list; only an order that is not there is a 404.
public sealed class ListPhotosQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<ListPhotosQuery, Result<IReadOnlyList<PhotoDto>>>
{
    public async Task<Result<IReadOnlyList<PhotoDto>>> ExecuteAsync(ListPhotosQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        IQueryable<Order> orders = db.Orders.AsNoTracking().WithNumber(user.CompanyId, query.OrderNumber);
        if (!await orders.AnyAsync(ct))
        {
            return OrderRequestErrors.OrderNotFound(query.OrderNumber);
        }

        List<PhotoDto> photos = await (
            from order in orders
            from shipment in order.Shipments
            from operation in shipment.Operations
            from photo in operation.Photos
            orderby shipment.Position, operation.AppliedAt, operation.Id, photo.UploadedAt, photo.Id
            select new PhotoDto(
                photo.Id,
                operation.Id,
                Shipment.LabelOf(order.Number, shipment.Position),
                operation.Type,
                photo.ContentType,
                photo.SizeBytes,
                photo.UploadedAt))
            .ToListAsync(ct);
        return photos;
    }
}
