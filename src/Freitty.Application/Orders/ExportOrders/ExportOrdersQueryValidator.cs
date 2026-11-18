using FluentValidation;

namespace Freitty.Application.Orders.ExportOrders;

public sealed class ExportOrdersQueryValidator : AbstractValidator<ExportOrdersQuery>
{
    public ExportOrdersQueryValidator()
    {
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.DatePreset).IsInEnum();
        RuleFor(query => query.HubId).MustBeAHubId();
        RuleFor(query => query.Search).MustBeSearchable();
    }
}
