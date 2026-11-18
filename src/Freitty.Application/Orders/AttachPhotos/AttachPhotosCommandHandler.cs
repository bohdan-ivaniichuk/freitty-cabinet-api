using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.AttachPhotos;

// The aggregate decides whether the photos may be attached before any byte is stored, so a refusal stores nothing.
// Then the bytes are written and the order saved: two writes that are not one transaction. When the save does not
// happen — a lost race or a failure — the bytes just written are deleted again; one that slips through is removed
// by the next rebuild of the sample data.
public sealed class AttachPhotosCommandHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IPhotoStorage photoStorage,
    TimeProvider clock)
    : ICommandHandler<AttachPhotosCommand, Result<AttachedPhotosDto>>
{
    // The demo has no sign-in, so this is the only bound on what anyone can store; the rebuild deletes them.
    public const int MaxUploadedPhotoCount = 200;

    public async Task<Result<AttachedPhotosDto>> ExecuteAsync(AttachPhotosCommand command, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        Order? order = await LoadOrderAsync(user.CompanyId, command.OrderNumber, ct);
        if (order is null)
        {
            return OrderRequestErrors.OrderNotFound(command.OrderNumber);
        }

        if (order.Version != command.Version)
        {
            return OrderRequestErrors.VersionConflict(order.Number, order.Version, command.Version);
        }

        // The aggregate first: a refusal it makes is final, while the bound lifts at the next rebuild. Nothing is
        // saved after a refusal, so the photos it attached in memory go nowhere.
        List<NewPhoto> newPhotos = await NameUploadsAsync(command.Files, ct);
        Result<PhotoAttachment> attachResult = order.AttachPhotos(command.OperationId, newPhotos, clock.GetUtcNow());
        if (attachResult.IsFailure)
        {
            return attachResult.Error;
        }

        if (await CountUploadedPhotosAsync(ct) + command.Files.Count > MaxUploadedPhotoCount)
        {
            return PhotoRequestErrors.UploadLimit(MaxUploadedPhotoCount);
        }

        if (!await StoreAndSaveAsync(command.Files, newPhotos, ct))
        {
            return OrderRequestErrors.ChangedConcurrently(order.Number);
        }

        return attachResult.Value.ToDto(order.Version);
    }

    // Tracked, with what AttachPhotos reads: every operation, whose stored count the limit checks, and the alerts it
    // may resolve; the photos already attached are not needed. Split queries read the order row first, so a write
    // that lands between the statements has already moved the version, and the save fails.
    private Task<Order?> LoadOrderAsync(Guid companyId, string orderNumber, CancellationToken cancellationToken)
    {
        return db.Orders
            .WithNumber(companyId, orderNumber)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Operations)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Alerts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Across every company: the bound is on what the demo stores, not on what one client does.
    private Task<int> CountUploadedPhotosAsync(CancellationToken cancellationToken)
    {
        return db.Orders
            .SelectMany(order => order.Shipments)
            .SelectMany(shipment => shipment.Operations)
            .SelectMany(operation => operation.Photos)
            .CountAsync(photo => photo.StorageKey.StartsWith(PhotoStorageKeys.UploadsPrefix), cancellationToken);
    }

    // The validator has already read the type of every file; it is read again here to be stored with it.
    private static async Task<List<NewPhoto>> NameUploadsAsync(
        IReadOnlyList<PhotoFile> files,
        CancellationToken cancellationToken)
    {
        var newPhotos = new List<NewPhoto>();
        foreach (PhotoFile file in files)
        {
            string contentType = await PhotoSignature.ReadContentTypeAsync(file, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"{file.FileName} reached the handler without a known image type; the validator refuses it.");
            newPhotos.Add(new NewPhoto(PhotoStorageKeys.Upload(Guid.CreateVersion7()), contentType, file.SizeBytes));
        }

        return newPhotos;
    }

    // The clean-up takes no token: it must run also when the request was cancelled halfway.
    private async Task<bool> StoreAndSaveAsync(
        IReadOnlyList<PhotoFile> files,
        List<NewPhoto> newPhotos,
        CancellationToken cancellationToken)
    {
        bool isSaved = false;
        try
        {
            for (int index = 0; index < files.Count; index++)
            {
                await using Stream content = files[index].OpenReadStream();
                await photoStorage.SaveAsync(
                    newPhotos[index].StorageKey,
                    content,
                    newPhotos[index].ContentType,
                    cancellationToken);
            }

            // Not the request's token: an abort during the commit could keep rows whose bytes the clean-up deletes.
            isSaved = await db.TrySaveChangesAsync(CancellationToken.None);
            return isSaved;
        }
        finally
        {
            if (!isSaved)
            {
                await photoStorage.DeleteAsync(newPhotos.Select(photo => photo.StorageKey), CancellationToken.None);
            }
        }
    }
}
