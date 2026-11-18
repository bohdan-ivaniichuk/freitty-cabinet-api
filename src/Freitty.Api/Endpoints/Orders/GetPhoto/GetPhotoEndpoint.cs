using FastEndpoints;
using Freitty.Api.Errors;
using Freitty.Api.Photos;
using Freitty.Application.Orders;
using Freitty.Application.Orders.AttachPhotos;
using Freitty.Application.Orders.GetPhoto;
using Freitty.Domain.Common;

namespace Freitty.Api.Endpoints.Orders.GetPhoto;

// The bytes as they were uploaded, streamed from storage. A photo never changes once stored, so a browser keeps it for
// a day without asking again; private, since it is a client's document.
internal sealed class GetPhotoEndpoint : EndpointWithoutRequest
{
    private const string CacheControl = "private, max-age=86400, immutable";

    // Another site's page cannot embed an uploaded image: the bytes are anyone's upload on a public demo.
    private const string CrossOriginResourcePolicy = "same-origin";

    public override void Configure()
    {
        Get(ApiRoutes.Orders.Photo);
        Options(endpoint => endpoint.RequireRateLimiting(PhotoLimits.ReadPolicy));
        Description(endpoint => endpoint
            .ClearDefaultProduces()
            .Produces(
                StatusCodes.Status200OK,
                typeof(byte[]),
                PhotoSignature.Jpeg,
                PhotoSignature.Png,
                PhotoSignature.WebP)
            .ProducesProblemDetails()
            .ProducesDomainProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
        Summary(summary =>
        {
            summary.Summary = "Get the image of a photo";
            summary.Description =
                "The image bytes with the content type found at upload, its length, and a cache header: a photo " +
                "never changes.";
            summary.Params[ApiRoutes.Orders.NumberParameter] = "The order number or draft number, e.g. FR001383.";
            summary.Params[ApiRoutes.Orders.PhotoIdParameter] = "The id of the photo, from the photo list.";
            summary.Responses[StatusCodes.Status200OK] = "The image.";
            summary.Responses[StatusCodes.Status400BadRequest] = "The photo id is not a UUID.";
            summary.Responses[StatusCodes.Status429TooManyRequests] =
                $"The demo has served {PhotoLimits.PhotoReadsPerWindow} images in the current minute; Retry-After " +
                "says when to try again.";
            summary.Responses[StatusCodes.Status404NotFound] =
                "The current company has no order with that number, or the order has no such photo; the code is " +
                $"{PhotoRequestErrors.PhotoNotFoundCode} either way.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetPhotoQuery(
            Route<string>(ApiRoutes.Orders.NumberParameter)!,
            Route<Guid>(ApiRoutes.Orders.PhotoIdParameter));
        Result<PhotoContentDto> photoResult = await query.ExecuteAsync(ct);
        if (photoResult.IsFailure)
        {
            await Send.ResultAsync(photoResult.Error.ToProblem());
            return;
        }

        PhotoContentDto photo = photoResult.Value;
        await using Stream content = photo.Content;
        HttpContext.Response.Headers.CacheControl = CacheControl;
        HttpContext.Response.Headers["Cross-Origin-Resource-Policy"] = CrossOriginResourcePolicy;
        await Send.StreamAsync(
            content,
            fileLengthBytes: photo.SizeBytes,
            contentType: photo.ContentType,
            cancellation: ct);
    }
}
