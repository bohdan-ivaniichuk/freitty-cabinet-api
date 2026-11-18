using FastEndpoints;
using Freitty.Api.Errors;
using Freitty.Application.Orders;
using Freitty.Application.Orders.SubmitOrder;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Api.Endpoints.Orders.SubmitOrder;

// The request record is bound from the JSON body alone, as in recording an operation; the number is read from the
// route.
internal sealed class SubmitOrderEndpoint : Endpoint<SubmitOrderRequest, SubmittedOrderDto>
{
    public override void Configure()
    {
        Post(ApiRoutes.Orders.Submit);
        RequestBinder(new RequestBinder<SubmitOrderRequest>(BindingSource.JsonBody));
        Description(endpoint => endpoint
            .ClearDefaultProduces()
            .Produces<SubmittedOrderDto>(StatusCodes.Status200OK)
            .ProducesProblemDetails()
            .ProducesDomainProblem(StatusCodes.Status404NotFound)
            .ProducesDomainProblem(StatusCodes.Status409Conflict));
        Summary(summary =>
        {
            summary.Summary = "Submit a draft";
            summary.Description =
                "Moves a draft to Ready under the next official number, FR followed by six digits; the draft " +
                "number no longer names the order. The aggregate decides whether the draft has everything a " +
                "submitted order needs. expectedVersion is the version of the order the caller loaded; any other " +
                "version is refused, so a repeated request never submits twice.";
            summary.RequestParam(
                request => request.ExpectedVersion,
                "The version of the order the caller loaded, from its card or its detail.");
            summary.Params[ApiRoutes.Orders.NumberParameter] =
                "The draft number, e.g. DRAFT-004; matched case-insensitively.";
            summary.ExampleRequest = new SubmitOrderRequest(ExpectedVersion: 6);
            summary.Responses[StatusCodes.Status200OK] = "Submitted. The body has the official number, the status " +
                "and the new version.";
            summary.Responses[StatusCodes.Status400BadRequest] = "The body is not understood, or expectedVersion " +
                "is missing or not positive.";
            summary.Responses[StatusCodes.Status404NotFound] =
                $"No such order in the current company ({OrderRequestErrors.OrderNotFoundCode}).";
            summary.Responses[StatusCodes.Status409Conflict] =
                "The order changed since the caller loaded it, or while the request ran " +
                $"({OrderRequestErrors.VersionConflictCode}); the order is not a draft ({OrderErrors.NotDraftCode}); " +
                "the draft lacks what a submitted order needs, which the detail lists " +
                $"({OrderErrors.NotReadyToSubmitCode}).";
        });
    }

    public override async Task HandleAsync(SubmitOrderRequest request, CancellationToken ct)
    {
        SubmitOrderCommand command = request.ToCommand(Route<string>(ApiRoutes.Orders.NumberParameter)!);
        Result<SubmittedOrderDto> submitResult = await command.ExecuteAsync(ct);
        if (submitResult.IsFailure)
        {
            await Send.ResultAsync(submitResult.Error.ToProblem());
            return;
        }

        await Send.OkAsync(submitResult.Value, ct);
    }
}
