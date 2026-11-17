using System.Diagnostics.CodeAnalysis;

namespace Freitty.Domain.Common;

public sealed class Result
{
    private static readonly Result SuccessResult = new(error: null);

    private Result(DomainError? error)
    {
        Error = error;
    }

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success()
    {
        return SuccessResult;
    }

    public static implicit operator Result(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }
}
