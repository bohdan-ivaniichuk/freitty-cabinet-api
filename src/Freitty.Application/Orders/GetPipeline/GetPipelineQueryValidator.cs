using FluentValidation;

namespace Freitty.Application.Orders.GetPipeline;

public sealed class GetPipelineQueryValidator : AbstractValidator<GetPipelineQuery>
{
    public GetPipelineQueryValidator()
    {
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.DatePreset).IsInEnum();
        RuleFor(query => query.HubId).MustBeAHubId();
        RuleFor(query => query.Search).MustBeSearchable();
    }
}
