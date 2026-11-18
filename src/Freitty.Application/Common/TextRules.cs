using FluentValidation;

namespace Freitty.Application.Common;

internal static class TextRules
{
    // PostgreSQL refuses a NUL in text outright, and no other control character belongs in a value a person types.
    public static IRuleBuilderOptions<T, string?> WithoutControlCharacters<T>(
        this IRuleBuilder<T, string?> rule,
        string subject)
    {
        return rule
            .Must(text => text is null || !text.Any(char.IsControl))
            .WithMessage($"{subject} must not contain control characters.");
    }
}
