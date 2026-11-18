using Freitty.Api.Health;
using Microsoft.AspNetCore.Diagnostics;

namespace Freitty.Api.Errors;

// Every error leaves as a ProblemDetails. FastEndpoints writes the 400s itself; an unknown route and an unhandled
// exception have no body of their own, so these give them one. The response never carries the exception.
internal static class ErrorResponses
{
    public static IServiceCollection AddErrorResponses(this IServiceCollection services)
    {
        services.AddProblemDetails();

        // The request log records the exception with its request, so the handler's own log line would repeat it.
        // Health requests are the exception: the request log keeps them below its level.
        services.Configure<ExceptionHandlerOptions>(options => options.SuppressDiagnosticsCallback =
            context => !HealthEndpoints.IsHealthRequest(context.HttpContext.Request));
        return services;
    }

    // Outermost, so that it turns every exception into a response.
    public static WebApplication UseErrorResponses(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
