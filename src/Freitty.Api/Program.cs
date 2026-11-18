using Freitty.Api.Endpoints;
using Freitty.Api.Errors;
using Freitty.Api.Health;
using Freitty.Api.Logging;
using Freitty.Api.OpenApi;
using Freitty.Api.Pages;
using Freitty.Api.Pipeline;
using Freitty.Api.Reporting;
using Freitty.Application;
using Freitty.Infrastructure;
using Freitty.Infrastructure.Persistence;
using Freitty.Infrastructure.Persistence.Seed;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddReportingTimeZone(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddErrorResponses();
builder.Services.AddApiEndpoints();
builder.Services.AddCommandPipeline();
builder.Services.AddApiDocumentation();
builder.Services.AddHealthEndpoints();

WebApplication app = builder.Build();

await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
await app.Services.SeedDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseErrorResponses();
app.UseStaticPages();
app.UseRequestLogging();
app.MapHealthEndpoints();
app.UseApiEndpoints();
app.MapApiDocumentation();

await app.RunAsync();
