using System.Diagnostics.CodeAnalysis;

namespace Freitty.Domain.Common;

public sealed class Result<T>
    where T : notnull
{
    private readonly T? value;

    private Result(T value)
    {
        this.value = value;
    }

    private Result(DomainError error)
    {
        Error = error;
    }

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException(
            $"A failed result has no value. Check IsSuccess first; the error was {Error.Code}: {Error.Message}");

    public static implicit operator Result<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }

    public static implicit operator Result<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}
