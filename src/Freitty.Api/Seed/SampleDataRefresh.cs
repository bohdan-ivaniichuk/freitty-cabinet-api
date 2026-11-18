using Freitty.Api.Health;
using Freitty.Infrastructure.Persistence.Seed;

namespace Freitty.Api.Seed;

internal static class SampleDataRefresh
{
    // Read while the services are registered: with Seed:Refresh off, or the seed itself off, neither the refresher
    // nor the request gate is registered and nothing runs. A setting that is not a boolean stops the startup here.
    public static IServiceCollection AddSampleDataRefresh(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (SeedSettings.Read(configuration).IsRefreshEnabled)
        {
            services.AddSingleton<RequestGate>();
            services.AddHostedService<SampleDataRefresher>();
        }

        return services;
    }

    // Placed after the static pages, which read no data, and after the request log, so time spent at the gate is in
    // the logged duration. The health checks pass by: a probe must answer while a rebuild runs.
    public static WebApplication UseSampleDataRefresh(this WebApplication app)
    {
        if (app.Services.GetService<RequestGate>() is { } requestGate)
        {
            app.Use(async (context, next) =>
            {
                if (HealthEndpoints.IsHealthRequest(context.Request))
                {
                    await next(context);
                    return;
                }

                using IDisposable entered = await requestGate.EnterAsync(context.RequestAborted);
                await next(context);
            });
        }

        return app;
    }
}
