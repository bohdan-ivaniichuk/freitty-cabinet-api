using Freitty.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Freitty.Api.Errors;

// A refusal of the domain or the application leaves as a ProblemDetails: the status follows the kind of the error,
// the message is the detail, and the stable code travels with it for a client to switch on.
internal static class DomainErrorProblem
{
    public static ProblemHttpResult ToProblem(this DomainError error)
    {
        return TypedResults.Problem(new DomainProblemDetails
        {
            Status = StatusCodeOf(error.Kind),
            Detail = error.Message,
            Code = error.Code,
        });
    }

    private static int StatusCodeOf(DomainErrorKind kind)
    {
        return kind switch
        {
            DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
            DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                $"DomainErrorKind.{kind} has no HTTP status; map it in {nameof(DomainErrorProblem)}."),
        };
    }
}
