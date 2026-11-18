namespace Freitty.Api.Errors;

// Documents a refusal in the OpenAPI document the way DomainErrorProblem sends it.
internal static class DomainProblemDescription
{
    private const string ProblemJson = "application/problem+json";

    public static RouteHandlerBuilder ProducesDomainProblem(this RouteHandlerBuilder endpoint, int statusCode)
    {
        return endpoint.Produces<DomainProblemDetails>(statusCode, ProblemJson);
    }
}
