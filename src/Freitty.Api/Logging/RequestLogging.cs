using Freitty.Api.Health;
using Serilog;
using Serilog.Events;

namespace Freitty.Api.Logging;

internal static class RequestLogging
{
    // Not a status the server sends — nobody is listening — but the one the request log records.
    private const int ClientClosedRequest = 499;

    public static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = SelectLevel);
        app.Use(EndAbandonedRequestAsync);
        return app;
    }

    // A client that goes away cancels its queries. That is not a failure of the server: the request ends here as
    // 499, and the request log records it at Information without the exception and its stack trace.
    private static async Task EndAbandonedRequestAsync(HttpContext httpContext, RequestDelegate next)
    {
        try
        {
            await next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = ClientClosedRequest;
            }
        }
    }

    // Health probes arrive every few seconds; logging them below the minimum level keeps real requests
    // readable. A failing readiness check is still logged by the health check service itself.
    private static LogEventLevel SelectLevel(HttpContext httpContext, double elapsedMilliseconds, Exception? exception)
    {
        if (HealthEndpoints.IsHealthRequest(httpContext.Request))
        {
            return LogEventLevel.Verbose;
        }

        bool failed = exception is not null
            || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError;
        return failed ? LogEventLevel.Error : LogEventLevel.Information;
    }
}
