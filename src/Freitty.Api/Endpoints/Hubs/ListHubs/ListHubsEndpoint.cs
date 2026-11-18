using FastEndpoints;
using Freitty.Application.Hubs.ListHubs;

namespace Freitty.Api.Endpoints.Hubs.ListHubs;

internal sealed class ListHubsEndpoint : EndpointWithoutRequest<IReadOnlyList<HubDto>>
{
    public override void Configure()
    {
        Get(ApiRoutes.Hubs.List);
        Summary(summary =>
        {
            summary.Summary = "List the hubs";
            summary.Description = "The options of the Hub filter, ordered by name.";
            summary.Responses[StatusCodes.Status200OK] = "Every hub of the platform.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        IReadOnlyList<HubDto> hubs = await new ListHubsQuery().ExecuteAsync(ct);
        await Send.OkAsync(hubs, ct);
    }
}
