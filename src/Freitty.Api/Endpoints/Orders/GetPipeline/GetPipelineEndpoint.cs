using FastEndpoints;
using Freitty.Application.Orders.GetPipeline;

namespace Freitty.Api.Endpoints.Orders.GetPipeline;

internal sealed class GetPipelineEndpoint : Endpoint<GetPipelineQuery, PipelineDto>
{
    public override void Configure()
    {
        Get(ApiRoutes.Orders.Pipeline);
        Description(endpoint => endpoint.ProducesProblemDetails());
        Summary(summary =>
        {
            summary.Summary = "Show the orders of the Order List as a board";
            summary.Description =
                "One column per status — Draft, Ready, InProgress, Completed, always all four and in that " +
                $"order — with the total of the column and its first {GetPipelineQuery.PipelineColumnSize} " +
                "cards in the order of the list. The filters are those of the list; a column whose status they " +
                "exclude is empty, with a total of 0.";
            summary.RequestParam(query => query.Type, OrderFilterDescriptions.Type);
            summary.RequestParam(query => query.Status, OrderFilterDescriptions.Status);
            summary.RequestParam(query => query.HasOpenAlerts, OrderFilterDescriptions.HasOpenAlerts);
            summary.RequestParam(query => query.HubId, OrderFilterDescriptions.HubId);
            summary.RequestParam(query => query.DatePreset, OrderFilterDescriptions.DatePreset);
            summary.RequestParam(query => query.Search, OrderFilterDescriptions.Search);
            summary.Responses[StatusCodes.Status200OK] = "The four columns with their totals and first cards.";
            summary.Responses[StatusCodes.Status400BadRequest] = "A parameter is out of range or not understood.";
        });
    }

    public override async Task HandleAsync(GetPipelineQuery query, CancellationToken ct)
    {
        PipelineDto pipeline = await query.ExecuteAsync(ct);
        await Send.OkAsync(pipeline, ct);
    }
}
