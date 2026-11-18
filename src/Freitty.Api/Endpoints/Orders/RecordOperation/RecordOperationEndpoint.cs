using FastEndpoints;
using Freitty.Api.Endpoints.Orders.GetOrderDetail;
using Freitty.Api.Errors;
using Freitty.Application.Orders;
using Freitty.Application.Orders.RecordOperation;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Api.Endpoints.Orders.RecordOperation;

// The request record is bound from the JSON body alone: FastEndpoints would otherwise let the query string replace
// a value of the body. The number is read from the route, as the detail endpoint reads it.
internal sealed class RecordOperationEndpoint : Endpoint<RecordOperationRequest, RecordedOperationDto>
{
    public override void Configure()
    {
        Post(ApiRoutes.Orders.Operations);
        RequestBinder(new RequestBinder<RecordOperationRequest>(BindingSource.JsonBody));
        Description(endpoint => endpoint
            .ClearDefaultProduces()
            .Produces<RecordedOperationDto>(StatusCodes.Status201Created)
            .ProducesProblemDetails()
            .ProducesDomainProblem(StatusCodes.Status404NotFound)
            .ProducesDomainProblem(StatusCodes.Status409Conflict));
        Summary(summary =>
        {
            summary.Summary = "Record an operation on a shipment";
            summary.Description =
                "Appends an Unloading, Disposal, Restack or Loading to the ledger of one shipment of a Ready or " +
                "InProgress order. The aggregate decides the rest in the same transaction: the first operation " +
                "starts the order, an Unloading opens or resolves a Quantity delta alert, an Unloading or a " +
                "Disposal opens a Missing photo alert, which uploading a photo to it closes. expectedVersion is the " +
                "version the caller loaded; any other version is refused, so a repeated request is never recorded " +
                "twice.";
            summary.RequestParam(
                request => request.Quantity,
                $"Pallets, from 1 to {RecordOperationCommandValidator.MaxQuantity}.");
            summary.RequestParam(
                request => request.TrailerNumber,
                "Required for Unloading and Loading, at most " +
                $"{Operation.MaxTrailerNumberLength} characters; null or left out for " +
                "Disposal and Restack.");
            summary.RequestParam(request => request.IsBillable, "False when left out.");
            summary.RequestParam(
                request => request.ExpectedVersion,
                "The version of the order the caller loaded, from its detail.");
            summary.Params[ApiRoutes.Orders.NumberParameter] =
                "The order number, e.g. FR001383; matched case-insensitively.";
            summary.ExampleRequest = new RecordOperationRequest(
                ShipmentPosition: 1,
                Type: OperationType.Loading,
                UnitType: UnitType.Standard,
                Quantity: 5,
                ExpectedVersion: 20,
                TrailerNumber: "TRL-8830",
                IsBillable: true);
            summary.Responses[StatusCodes.Status201Created] =
                "Recorded. The body identifies the operation and the order's new status and version; Location is " +
                "the order.";
            summary.Responses[StatusCodes.Status400BadRequest] = "A field is missing, out of range or not understood.";
            summary.Responses[StatusCodes.Status404NotFound] =
                $"No such order in the current company ({OrderRequestErrors.OrderNotFoundCode}), or no shipment at " +
                $"that position ({OrderErrors.ShipmentNotFoundCode}).";
            summary.Responses[StatusCodes.Status409Conflict] =
                "The order changed since the caller loaded it, or while the request ran " +
                $"({OrderRequestErrors.VersionConflictCode}); the order is not Ready or InProgress " +
                $"({OrderErrors.NotActiveCode}); more than is on stock ({OrderErrors.ExceedsStockCode}).";
        });
    }

    public override async Task HandleAsync(RecordOperationRequest request, CancellationToken ct)
    {
        RecordOperationCommand command = request.ToCommand(Route<string>(ApiRoutes.Orders.NumberParameter)!);
        Result<RecordedOperationDto> recordResult = await command.ExecuteAsync(ct);
        if (recordResult.IsFailure)
        {
            await Send.ResultAsync(recordResult.Error.ToProblem());
            return;
        }

        RecordedOperationDto recorded = recordResult.Value;
        await Send.CreatedAtAsync<GetOrderDetailEndpoint>(
            new RouteValueDictionary { [ApiRoutes.Orders.NumberParameter] = recorded.Order.Number },
            recorded,
            cancellation: ct);
    }
}
