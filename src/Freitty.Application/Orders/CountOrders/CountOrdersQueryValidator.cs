using FluentValidation;

namespace Freitty.Application.Orders.CountOrders;

public sealed class CountOrdersQueryValidator : AbstractValidator<CountOrdersQuery>
{
    public CountOrdersQueryValidator()
    {
        RuleFor(query => query.Date).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Hub).MustBeAHubId();
        RuleFor(query => query.Q).MustBeSearchable();
    }
}
