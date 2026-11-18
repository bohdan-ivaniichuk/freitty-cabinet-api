using FastEndpoints;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetPipeline;

// The filters of the list, without its page: the board shows the first cards of every status at once.
public sealed record GetPipelineQuery(
    OrderType? Type = null,
    OrderStatus? Status = null,
    bool HasOpenAlerts = false,
    Guid? HubId = null,
    DatePreset? DatePreset = null,
    string? Search = null)
    : ICommand<PipelineDto>
{
    public const int PipelineColumnSize = 5;
}
