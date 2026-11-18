using Freitty.Domain.Common;

namespace Freitty.Api.IntegrationTests.Support;

// Arrange steps must not fail silently: a refused setup call would let a later assertion pass for the wrong reason.
internal static class ResultExtensions
{
    public static void MustSucceed(this Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"A setup call was refused with {result.Error.Code}: {result.Error.Message}");
        }
    }

    public static T MustSucceed<T>(this Result<T> result)
        where T : notnull
    {
        return result.Value;
    }
}
