using FluentValidation;

namespace Freitty.Application.Orders.SubmitOrder;

public sealed class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
{
    public SubmitOrderCommandValidator()
    {
        RuleFor(command => command.ExpectedVersion).GreaterThanOrEqualTo(1);
    }
}
