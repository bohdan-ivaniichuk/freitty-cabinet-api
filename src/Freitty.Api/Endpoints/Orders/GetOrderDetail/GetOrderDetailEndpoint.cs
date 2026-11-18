using FastEndpoints;
using Freitty.Api.Errors;
using Freitty.Application.Orders;
using Freitty.Application.Orders.GetOrderDetail;
using Freitty.Domain.Common;

namespace Freitty.Api.Endpoints.Orders.GetOrderDetail;

// Unlike its neighbours, this endpoint does not bind the query: FastEndpoints binds a request from the route and then
// from the query string, so ?number=… would replace the number of the route. The number is read from the route only.
internal sealed class GetOrderDetailEndpoint : EndpointWithoutRequest<OrderDetailDto>
{
    public override void Configure()
    {
        Get(ApiRoutes.Orders.Detail);
        Description(endpoint => endpoint.ProducesDomainProblem(StatusCodes.Status404NotFound));
        Summary(summary =>
        {
            summary.Summary = "Get the detail of an order";
            summary.Description =
                "One order with everything the detail page shows: its header, its shipments with arrival, dock, " +
                "quantities, alerts and operations, its supplies, services and invoices. The number is matched " +
                "as a whole and case-insensitively. Instants are UTC; the hub's time zone travels with them.";
            summary.Params[ApiRoutes.Orders.NumberParameter] = "The order number or draft number, e.g. FR001383.";
            summary.Responses[StatusCodes.Status200OK] = "The order.";
            summary.Responses[StatusCodes.Status404NotFound] =
                "The current company has no order with that number; the code is " +
                $"{OrderRequestErrors.OrderNotFoundCode}.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetOrderDetailQuery(Route<string>(ApiRoutes.Orders.NumberParameter)!);
        Result<OrderDetailDto> detailResult = await query.ExecuteAsync(ct);
        if (detailResult.IsFailure)
        {
            await Send.ResultAsync(detailResult.Error.ToProblem());
            return;
        }

        await Send.OkAsync(detailResult.Value, ct);
    }
}
