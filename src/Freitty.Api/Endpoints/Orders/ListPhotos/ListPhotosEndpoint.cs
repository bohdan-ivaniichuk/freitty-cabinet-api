using FastEndpoints;
using Freitty.Api.Errors;
using Freitty.Application.Orders;
using Freitty.Application.Orders.ListPhotos;
using Freitty.Domain.Common;

namespace Freitty.Api.Endpoints.Orders.ListPhotos;

// The number is read from the route only, as the detail reads it.
internal sealed class ListPhotosEndpoint : EndpointWithoutRequest<IReadOnlyList<PhotoDto>>
{
    public override void Configure()
    {
        Get(ApiRoutes.Orders.Photos);
        Description(endpoint => endpoint.ProducesDomainProblem(StatusCodes.Status404NotFound));
        Summary(summary =>
        {
            summary.Summary = "List the photos of an order";
            summary.Description =
                "Every photo of the order in the order of its page: by shipment position, then by the time of the " +
                "operation, then by upload time. The bytes of each are served by the photo's own route.";
            summary.Params[ApiRoutes.Orders.NumberParameter] = "The order number or draft number, e.g. FR001383.";
            summary.Responses[StatusCodes.Status200OK] = "The photos; empty when the order has none.";
            summary.Responses[StatusCodes.Status404NotFound] =
                "The current company has no order with that number; the code is " +
                $"{OrderRequestErrors.OrderNotFoundCode}.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new ListPhotosQuery(Route<string>(ApiRoutes.Orders.NumberParameter)!);
        Result<IReadOnlyList<PhotoDto>> listResult = await query.ExecuteAsync(ct);
        if (listResult.IsFailure)
        {
            await Send.ResultAsync(listResult.Error.ToProblem());
            return;
        }

        await Send.OkAsync(listResult.Value, ct);
    }
}
