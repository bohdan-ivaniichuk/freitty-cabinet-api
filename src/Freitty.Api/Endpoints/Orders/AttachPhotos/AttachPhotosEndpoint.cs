using FastEndpoints;
using Freitty.Api.Endpoints.Orders.ListPhotos;
using Freitty.Api.Errors;
using Freitty.Api.Photos;
using Freitty.Application.Orders;
using Freitty.Application.Orders.AttachPhotos;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Freitty.Api.Endpoints.Orders.AttachPhotos;

// The request is bound from the form alone: the query string could otherwise replace the version of the form. The
// number and the operation are read from the route. The switch of the demo is checked here, before the command: it is
// a setting of the deployment, not a rule of the order.
internal sealed class AttachPhotosEndpoint(IOptionsMonitor<PhotoUploadSettings> uploadSettings)
    : Endpoint<AttachPhotosRequest, AttachedPhotosDto>
{
    public const string UploadsDisabledCode = "photo.uploads_disabled";
    private const string UploadsDisabledDetail =
        "Uploading photos is switched off on this demo; the photos already stored can still be viewed.";

    public override void Configure()
    {
        Post(ApiRoutes.Orders.OperationPhotos);
        AllowFileUploads();
        RequestBinder(new RequestBinder<AttachPhotosRequest>(BindingSource.FormFields));
        Options(endpoint => endpoint
            .RequireRateLimiting(PhotoLimits.UploadPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(PhotoLimits.MaxUploadBodyBytes)));
        Description(endpoint => endpoint
            .ClearDefaultProduces()
            .Produces<AttachedPhotosDto>(StatusCodes.Status201Created)
            .ProducesProblemDetails()
            .ProducesDomainProblem(StatusCodes.Status404NotFound)
            .ProducesDomainProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesDomainProblem(StatusCodes.Status503ServiceUnavailable));
        Summary(summary =>
        {
            summary.Summary = "Upload photos of an operation";
            summary.Description =
                $"Stores one to {AttachPhotosCommandValidator.MaxFileCount} photos and attaches them to an " +
                "operation of a Ready or InProgress order, as multipart/form-data with the fields version and " +
                "files. A file is accepted when its content is a JPEG, PNG or WebP image of at most " +
                $"{PhotoFileValidator.MaxSizeText}; the type found in the bytes is the one stored. Once every " +
                "Unloading and Disposal of the shipment has a photo, its Missing photo alert is resolved. version " +
                "is the version of the order the caller loaded; any other version is refused and nothing is stored.";
            summary.RequestParam(request => request.Version, "The version of the order the caller loaded.");
            summary.RequestParam(
                request => request.Files,
                $"One to {AttachPhotosCommandValidator.MaxFileCount} JPEG, PNG or WebP files of at most " +
                $"{PhotoFileValidator.MaxSizeText} each.");
            summary.Params[ApiRoutes.Orders.NumberParameter] =
                "The order number, e.g. FR001383; matched case-insensitively.";
            summary.Params[ApiRoutes.Orders.OperationIdParameter] = "The id of the operation, from the detail.";
            summary.Responses[StatusCodes.Status201Created] =
                "Attached. The body has the photos just added, the operation's new photo count, the order's new " +
                "version and the alerts this closed; Location is the photos of the order.";
            summary.Responses[StatusCodes.Status400BadRequest] =
                $"No file, more than {AttachPhotosCommandValidator.MaxFileCount}, a file over " +
                $"{PhotoFileValidator.MaxSizeText} or one that is not a JPEG, PNG or WebP image — the problem " +
                "names the file — version missing or not positive, or an operation id that is not a UUID.";
            summary.Responses[StatusCodes.Status404NotFound] =
                $"No such order in the current company ({OrderRequestErrors.OrderNotFoundCode}), or no such " +
                $"operation on it ({OrderErrors.OperationNotFoundCode}).";
            summary.Responses[StatusCodes.Status409Conflict] =
                "The order changed since the caller loaded it, or while the request ran " +
                $"({OrderRequestErrors.VersionConflictCode}); the order is not Ready or InProgress " +
                $"({OrderErrors.NotActiveCode}); the operation would have more than {Operation.MaxPhotoCount} " +
                $"photos ({OrderErrors.PhotoLimitCode}); the demo already holds " +
                $"{AttachPhotosCommandHandler.MaxUploadedPhotoCount} uploaded photos, which the next rebuild of " +
                $"the sample data deletes ({PhotoRequestErrors.UploadLimitCode}).";
            summary.Responses[StatusCodes.Status415UnsupportedMediaType] = "The body is not multipart/form-data.";
            summary.Responses[StatusCodes.Status429TooManyRequests] =
                $"{PhotoLimits.ConcurrentUploads} uploads are already running on the demo; try again in a moment.";
            summary.Responses[StatusCodes.Status503ServiceUnavailable] =
                $"Uploads are switched off on this demo ({UploadsDisabledCode}).";
        });
    }

    // The body has no order number, so Location names the order as the request did; the number matches either way.
    public override async Task HandleAsync(AttachPhotosRequest request, CancellationToken ct)
    {
        if (!uploadSettings.CurrentValue.UploadsEnabled)
        {
            await Send.ResultAsync(TypedResults.Problem(new DomainProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = UploadsDisabledDetail,
                Code = UploadsDisabledCode,
            }));
            return;
        }

        string orderNumber = Route<string>(ApiRoutes.Orders.NumberParameter)!;
        AttachPhotosCommand command =
            request.ToCommand(orderNumber, Route<Guid>(ApiRoutes.Orders.OperationIdParameter));
        Result<AttachedPhotosDto> attachResult = await command.ExecuteAsync(ct);
        if (attachResult.IsFailure)
        {
            await Send.ResultAsync(attachResult.Error.ToProblem());
            return;
        }

        await Send.CreatedAtAsync<ListPhotosEndpoint>(
            new RouteValueDictionary { [ApiRoutes.Orders.NumberParameter] = orderNumber },
            attachResult.Value,
            cancellation: ct);
    }
}
