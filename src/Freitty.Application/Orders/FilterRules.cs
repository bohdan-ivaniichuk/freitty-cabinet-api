using FluentValidation;
using Freitty.Application.Common;

namespace Freitty.Application.Orders;

// The rules of the filters the list, the counters and the export share.
public static class FilterRules
{
    public const int MinSearchLength = 3;
    public const int MaxSearchLength = 50;

    // Absent, empty or white space is no search. Otherwise the trimmed text must be long enough for a trigram index
    // to serve it.
    public static IRuleBuilderOptions<T, string?> MustBeSearchable<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule
            .Must(text => string.IsNullOrWhiteSpace(text)
                || text.Trim().Length is >= MinSearchLength and <= MaxSearchLength)
            .WithMessage(
                $"Search text must be {MinSearchLength} to {MaxSearchLength} characters long, spaces around it aside.")
            .WithoutControlCharacters("Search text");
    }

    // An empty "hubId=" binds as the empty GUID, which would silently match no order.
    public static IRuleBuilderOptions<T, Guid?> MustBeAHubId<T>(this IRuleBuilder<T, Guid?> rule)
    {
        return rule
            .NotEqual(Guid.Empty)
            .WithMessage("Hub id must be the id of a hub; leave the parameter out for every hub.");
    }
}
