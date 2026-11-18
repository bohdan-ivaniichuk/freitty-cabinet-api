using FastEndpoints;
using Freitty.Application.Me.GetMe;

namespace Freitty.Api.Endpoints.Me.Get;

internal sealed class GetMeEndpoint : EndpointWithoutRequest<MeDto>
{
    public override void Configure()
    {
        Get("me");
        Summary(summary =>
        {
            summary.Summary = "Get the current user";
            summary.Description = "The user the cabinet acts as, the user's company, and needAttention — the number on the Orders badge, " +
                "equal to the Need attention total of the dashboard — for the page shell.";
            summary.Responses[StatusCodes.Status200OK] = "The user and the company.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        MeDto me = await new GetMeQuery().ExecuteAsync(ct);
        await Send.OkAsync(me, ct);
    }
}
