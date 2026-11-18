using FastEndpoints;
using Freitty.Application.Orders.CountOrders;

namespace Freitty.Api.Endpoints.Orders.Counts;

internal sealed class CountOrdersEndpoint : Endpoint<CountOrdersQuery, OrderCountsDto>
{
    public override void Configure()
    {
        Get("orders/counts");
        Description(endpoint => endpoint.ProducesProblemDetails());
        Summary(summary =>
        {
            summary.Summary = "Count the orders of each Order List tab";
            summary.Description =
                "Each counter is the total the list returns for its tab under the same hub, date, status and " +
                "search. The Drafts counter ignores the status filter.";
            summary.Responses[StatusCodes.Status200OK] = "The counter of each tab.";
            summary.Responses[StatusCodes.Status400BadRequest] = "A parameter is out of range or not understood.";
        });
    }

    public override async Task HandleAsync(CountOrdersQuery query, CancellationToken ct)
    {
        OrderCountsDto counts = await query.ExecuteAsync(ct);
        await Send.OkAsync(counts, ct);
    }
}
