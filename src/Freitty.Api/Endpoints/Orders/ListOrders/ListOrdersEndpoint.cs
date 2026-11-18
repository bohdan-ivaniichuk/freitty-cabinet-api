using FastEndpoints;
using Freitty.Application.Orders.ListOrders;

namespace Freitty.Api.Endpoints.Orders.ListOrders;

internal sealed class ListOrdersEndpoint : Endpoint<ListOrdersQuery, OrderPageDto>
{
    public override void Configure()
    {
        Get(ApiRoutes.Orders.List);
        Description(endpoint => endpoint.ProducesProblemDetails());
        Summary(summary =>
        {
            summary.Summary = "List the orders of the Order List";
            summary.Description =
                "One page of order cards, newest Scheduled date first with unscheduled drafts on top. The tabs " +
                "are filters: type for Cross-Dock and Consolidation, hasOpenAlerts for Alerts, and status Draft " +
                "for Drafts.";
            summary.RequestParam(query => query.Type, OrderFilterDescriptions.Type);
            summary.RequestParam(query => query.Status, OrderFilterDescriptions.Status);
            summary.RequestParam(query => query.HasOpenAlerts, OrderFilterDescriptions.HasOpenAlerts);
            summary.RequestParam(query => query.HubId, OrderFilterDescriptions.HubId);
            summary.RequestParam(query => query.DatePreset, OrderFilterDescriptions.DatePreset);
            summary.RequestParam(query => query.Search, OrderFilterDescriptions.Search);
            summary.RequestParam(
                query => query.Page,
                $"The page, from {ListOrdersQuery.FirstPage} to {ListOrdersQueryValidator.MaxPage}; " +
                $"{ListOrdersQuery.FirstPage} when left out.");
            summary.RequestParam(
                query => query.PageSize,
                $"Cards per page, from 1 to {ListOrdersQueryValidator.MaxPageSize}; " +
                $"{ListOrdersQuery.DefaultPageSize} when left out.");
            summary.Responses[StatusCodes.Status200OK] = "A page of cards and the total of the filtered list.";
            summary.Responses[StatusCodes.Status400BadRequest] = "A parameter is out of range or not understood.";
        });
    }

    public override async Task HandleAsync(ListOrdersQuery query, CancellationToken ct)
    {
        OrderPageDto page = await query.ExecuteAsync(ct);
        await Send.OkAsync(page, ct);
    }
}
