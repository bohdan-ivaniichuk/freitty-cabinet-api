using Freitty.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Freitty.Api.Health;

// Health is an operational contract for the container runtime, not part of the API: it lives outside
// /api/v1 and outside OpenAPI. Liveness checks nothing, so a database outage marks the instance
// not ready without making the platform restart it.
internal static class HealthEndpoints
{
    private const string ReadinessTag = "ready";
    private static readonly PathString HealthRoot = "/health";

    public static IServiceCollection AddHealthEndpoints(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: [ReadinessTag]);
        return services;
    }

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks($"{HealthRoot}/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks(
            $"{HealthRoot}/ready",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessTag) });
        return app;
    }

    public static bool IsHealthRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments(HealthRoot);
    }
}
