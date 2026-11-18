using FluentValidation;

namespace Freitty.Application.Orders;

// The rules of the filters the list and the counters share.
public static class FilterRules
{
    public const int MinSearchLength = 3;
    public const int MaxSearchLength = 50;

    // Absent, empty or white space is no search. Otherwise the trimmed text must be long enough for a trigram index
    // to serve it, and free of control characters: PostgreSQL refuses a NUL in text outright.
    public static IRuleBuilderOptions<T, string?> MustBeSearchable<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(text => string.IsNullOrWhiteSpace(text)
                || text.Trim().Length is >= MinSearchLength and <= MaxSearchLength)
            .WithMessage(
                $"Search text must be {MinSearchLength} to {MaxSearchLength} characters long, spaces around it aside.")
            .Must(text => text is null || !text.Any(char.IsControl))
            .WithMessage("Search text must not contain control characters.");
    }

    // An empty "hub=" binds as the empty GUID, which would silently match no order.
    public static IRuleBuilderOptions<T, Guid?> MustBeAHubId<T>(this IRuleBuilder<T, Guid?> rule)
    {
        return rule
            .NotEqual(Guid.Empty)
            .WithMessage("Hub must be the id of a hub; leave the parameter out for every hub.");
    }
}
