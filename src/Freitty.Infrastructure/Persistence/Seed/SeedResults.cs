using Freitty.Domain.Common;

namespace Freitty.Infrastructure.Persistence.Seed;

// Sample data that breaks a domain rule is a bug in the seed, not a decision of a user: it stops the startup like
// a failed migration.
internal static class SeedResults
{
    public static void OrThrow(this Result result)
    {
        if (result.IsFailure)
        {
            throw Refused(result.Error);
        }
    }

    public static T OrThrow<T>(this Result<T> result)
        where T : notnull
    {
        if (result.IsFailure)
        {
            throw Refused(result.Error);
        }

        return result.Value;
    }

    private static InvalidOperationException Refused(DomainError error)
    {
        return new InvalidOperationException($"The sample data breaks a domain rule: {error.Message}");
    }
}
