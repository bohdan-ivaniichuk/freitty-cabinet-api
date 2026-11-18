using Freitty.Application.Abstractions;
using Freitty.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Freitty.Api.IntegrationTests;

// An HTTP test passes its own current user: in the shared database the default one would find some other test's
// Admin.
internal sealed class ApiFactory(
    string connectionString,
    bool seedEnabled = false,
    TimeProvider? clock = null,
    ICurrentUser? currentUser = null,
    string? reportingTimeZone = null,
    IInterceptor? saveInterceptor = null)
    : WebApplicationFactory<Program>
{
    private const string ConnectionStringKey = "ConnectionStrings:Postgres";
    private const string SeedEnabledKey = "Seed:Enabled";
    private const string ReportingTimeZoneKey = "Cabinet:TimeZone";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, as in the container: appsettings.Development.json and its local connection string
        // are not loaded, so the test controls the connection string completely.
        builder.UseEnvironment(Environments.Production);

        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the connection string before
        // configuration callbacks run. It also wins over a ConnectionStrings__Postgres variable in the
        // developer's shell, so an empty value reliably means "missing".
        builder.UseSetting(ConnectionStringKey, connectionString);

        // Left absent when off, as in appsettings.json: that default is what the seed tests check.
        if (seedEnabled)
        {
            builder.UseSetting(SeedEnabledKey, bool.TrueString);
        }

        if (reportingTimeZone is not null)
        {
            builder.UseSetting(ReportingTimeZoneKey, reportingTimeZone);
        }

        builder.ConfigureTestServices(services =>
        {
            if (clock is not null)
            {
                services.AddSingleton(clock);
            }

            if (currentUser is not null)
            {
                services.AddSingleton(currentUser);
            }

            if (saveInterceptor is not null)
            {
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(saveInterceptor));
            }
        });
    }
}
