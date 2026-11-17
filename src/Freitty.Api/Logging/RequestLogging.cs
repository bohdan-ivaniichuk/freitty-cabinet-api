using Freitty.Api.Health;
using Serilog;
using Serilog.Events;

namespace Freitty.Api.Logging;

internal static class RequestLogging
{
    public static WebApplication UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = SelectLevel);
        return app;
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
