using Freitty.Api.Health;
using Freitty.Api.Logging;
using Freitty.Infrastructure;
using Freitty.Infrastructure.Persistence;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthEndpoints();

WebApplication app = builder.Build();

await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseRequestLogging();
app.MapHealthEndpoints();

await app.RunAsync();
