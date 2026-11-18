using FluentValidation;

namespace Freitty.Application.Orders.CountOrders;

public sealed class CountOrdersQueryValidator : AbstractValidator<CountOrdersQuery>
{
    public CountOrdersQueryValidator()
    {
        RuleFor(query => query.DatePreset).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.HubId).MustBeAHubId();
        RuleFor(query => query.Search).MustBeSearchable();
    }
}
