using FluentValidation;

namespace Freitty.Application.Orders.ListOrders;

public sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public const int MaxPageSize = 50;

    // Keeps the offset, (page - 1) * pageSize, inside an int.
    public const int MaxPage = 10_000;

    public ListOrdersQueryValidator()
    {
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.DatePreset).IsInEnum();
        RuleFor(query => query.HubId).MustBeAHubId();
        RuleFor(query => query.Search).MustBeSearchable();
        RuleFor(query => query.Page).InclusiveBetween(ListOrdersQuery.FirstPage, MaxPage);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
