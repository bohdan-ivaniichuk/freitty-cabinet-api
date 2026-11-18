using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.SubmitOrder;

// The aggregate decides whether the draft can be submitted; this finds it, checks the version the caller saw, asks
// the aggregate first, then takes the next official number and saves. A refused submit takes no number; only a submit
// that loses the race to another write leaves a gap.
public sealed class SubmitOrderCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IOrderNumbers orderNumbers,
    TimeProvider clock)
    : ICommandHandler<SubmitOrderCommand, Result<SubmittedOrderDto>>
{
    public async Task<Result<SubmittedOrderDto>> ExecuteAsync(SubmitOrderCommand command, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        Order? order = await LoadOrderAsync(user.CompanyId, command.OrderNumber, ct);
        if (order is null)
        {
            return OrderRequestErrors.OrderNotFound(command.OrderNumber);
        }

        if (order.Version != command.ExpectedVersion)
        {
            return OrderRequestErrors.VersionConflict(order.Number, order.Version, command.ExpectedVersion);
        }

        Result check = order.CanSubmit();
        if (check.IsFailure)
        {
            return check.Error;
        }

        string draftNumber = order.Number;
        string officialNumber = await orderNumbers.NextAsync(ct);
        Result submitResult = order.Submit(officialNumber, clock.GetUtcNow());
        if (submitResult.IsFailure)
        {
            return submitResult.Error;
        }

        if (!await db.TrySaveChangesAsync(ct))
        {
            return OrderRequestErrors.ChangedConcurrently(draftNumber);
        }

        return order.ToSubmittedOrder();
    }

    // Tracked, with what Submit reads: the shipments and their Cargo Lines. Split queries read the order row first,
    // so a write that lands between the statements has already moved the version, and the save fails.
    private Task<Order?> LoadOrderAsync(Guid companyId, string orderNumber, CancellationToken cancellationToken)
    {
        return db.Orders
            .WithNumber(companyId, orderNumber)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.CargoLines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
