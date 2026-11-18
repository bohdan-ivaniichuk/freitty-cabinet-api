using FluentValidation;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.RecordOperation;

// The shape of the request, nothing about the state of the order. Each rule keeps out a value the aggregate or the
// database would reject as a broken contract, which would be a 500.
// The two trailer conditions cover every defined type between them.
public sealed class RecordOperationCommandValidator : AbstractValidator<RecordOperationCommand>
{
    // The ledger sums quantities as int, and the endpoint is public.
    public const int MaxQuantity = 10_000;

    public RecordOperationCommandValidator()
    {
        RuleFor(command => command.ShipmentPosition).GreaterThanOrEqualTo(1);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.UnitType).IsInEnum();
        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, MaxQuantity)
            .WithMessage($"Quantity must be a whole number from 1 to {MaxQuantity}.");
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);

        RuleFor(command => command.TrailerNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(command => $"The trailer number is required for {command.Type}.")
            .MaximumLength(Operation.MaxTrailerNumberLength)
            .WithoutControlCharacters("The trailer number")
            .When(command => Operation.RequiresTrailer(command.Type));
        RuleFor(command => command.TrailerNumber)
            .Null()
            .WithMessage(command => $"{command.Type} takes no trailer number.")
            .When(command => Enum.IsDefined(command.Type) && !Operation.RequiresTrailer(command.Type));
    }
}
