using Microsoft.AspNetCore.Mvc;

namespace Freitty.Api.Errors;

// The body of a refusal: a ProblemDetails with the stable code a client switches on. One type both writes the body
// and describes it in the OpenAPI document.
internal sealed class DomainProblemDetails : ProblemDetails
{
    public required string Code { get; init; }
}
