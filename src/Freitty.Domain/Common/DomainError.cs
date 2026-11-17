namespace Freitty.Domain.Common;

public sealed record DomainError(string Code, string Message, DomainErrorKind Kind)
{
    public static DomainError NotFound(string code, string message)
    {
        return new DomainError(code, message, DomainErrorKind.NotFound);
    }

    public static DomainError Conflict(string code, string message)
    {
        return new DomainError(code, message, DomainErrorKind.Conflict);
    }
}
