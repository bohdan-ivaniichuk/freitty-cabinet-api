using FluentValidation;

namespace Freitty.Application.Dashboard.GetDashboard;

public sealed class GetDashboardQueryValidator : AbstractValidator<GetDashboardQuery>
{
    public GetDashboardQueryValidator()
    {
        RuleFor(query => query.Period).IsInEnum();
    }
}
