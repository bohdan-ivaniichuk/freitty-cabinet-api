namespace Freitty.Domain.Common;

// What Result and Result<T> share, for code that handles the outcome of any operation.
public interface IDomainResult
{
    DomainError? Error { get; }
}
