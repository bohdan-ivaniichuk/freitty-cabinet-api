using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Common;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.RecordOperation;

// The aggregate decides; this finds the order, checks the version the caller saw, resolves the shipment and saves
// once — the Operation, the Alert it raises or resolves, the status and the order row in one transaction.
public sealed class RecordOperationCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RecordOperationCommand, Result<RecordedOperationDto>>
{
    public async Task<Result<RecordedOperationDto>> ExecuteAsync(RecordOperationCommand command, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        Order? order = await LoadOrderAsync(user.CompanyId, command.Number, ct);
        if (order is null)
        {
            return ApplicationErrors.OrderNotFound(command.Number);
        }

        if (order.Version != command.ExpectedVersion)
        {
            return ApplicationErrors.VersionConflict(order.Number, order.Version, command.ExpectedVersion);
        }

        Shipment? shipment = order.Shipments.FirstOrDefault(
            candidate => candidate.Position == command.ShipmentPosition);
        if (shipment is null)
        {
            return ApplicationErrors.ShipmentNotFound(order.Number, command.ShipmentPosition);
        }

        Result<Operation> recording = order.RecordOperation(shipment.Id, command.ToNewOperation(), clock.GetUtcNow());
        if (recording.IsFailure)
        {
            return recording.Error;
        }

        if (!await db.TrySaveChangesAsync(ct))
        {
            return ApplicationErrors.ChangedConcurrently(order.Number);
        }

        return new RecordedOperationDto(recording.Value.ToDto(), order.ToRecordedOrder());
    }

    // Tracked, with what RecordOperation reads: without the Cargo Lines every delta would be computed against a
    // declared quantity of zero. Split queries read the order row first, so a write that lands between the statements
    // has already moved the version, and the save fails. The number is matched as the detail matches it.
    private Task<Order?> LoadOrderAsync(Guid companyId, string number, CancellationToken cancellationToken)
    {
        string lowerCaseNumber = number.ToLowerInvariant();
        return db.Orders
            .Where(order => order.CompanyId == companyId && order.Number.ToLower() == lowerCaseNumber)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.CargoLines)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Operations)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Alerts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
