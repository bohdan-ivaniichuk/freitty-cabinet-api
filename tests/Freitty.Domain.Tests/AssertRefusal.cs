using Freitty.Domain.Common;

namespace Freitty.Domain.Tests;

internal static class AssertRefusal
{
    public static DomainError Conflict(DomainError? error, string expectedCode)
    {
        return Is(error, expectedCode, DomainErrorKind.Conflict);
    }

    public static DomainError NotFound(DomainError? error, string expectedCode)
    {
        return Is(error, expectedCode, DomainErrorKind.NotFound);
    }

    private static DomainError Is(DomainError? error, string expectedCode, DomainErrorKind expectedKind)
    {
        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(expectedKind, error.Kind);
        return error;
    }
}
