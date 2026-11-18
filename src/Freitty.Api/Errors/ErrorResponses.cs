using Freitty.Api.Health;
using Freitty.Api.Pages;
using Microsoft.AspNetCore.Diagnostics;

namespace Freitty.Api.Errors;

// Every error leaves as a ProblemDetails. FastEndpoints writes the 400s itself; an unknown route and an unhandled
// exception have no body of their own, so these give them one. The response never carries the exception.
internal static class ErrorResponses
{
    public static IServiceCollection AddErrorResponses(this IServiceCollection services)
    {
        // The path of the request, as FastEndpoints puts it into its 400s.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path);

        // The request log records the exception with its request, so the handler's own log line would repeat it.
        // Health requests are the exception: the request log keeps them below its level.
        services.Configure<ExceptionHandlerOptions>(options => options.SuppressDiagnosticsCallback =
            context => !HealthEndpoints.IsHealthRequest(context.HttpContext.Request));
        return services;
    }

    // Outermost, so that it turns every exception into a response. A browser asking for a page that is not there gets
    // the page that says so; every other status without a body gets its ProblemDetails, as UseStatusCodePages writes
    // it by default.
    public static WebApplication UseErrorResponses(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages(context => NotFoundPage.IsFor(context.HttpContext)
            ? NotFoundPage.WriteAsync(context.HttpContext)
            : WriteProblemAsync(context.HttpContext));
        return app;
    }

    private static async Task WriteProblemAsync(HttpContext context)
    {
        IProblemDetailsService problemDetails = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = context.Response.StatusCode },
        });
    }
}
