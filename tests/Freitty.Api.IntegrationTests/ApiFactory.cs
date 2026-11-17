using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Freitty.Api.IntegrationTests;

internal sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private const string ConnectionStringKey = "ConnectionStrings:Postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, as in the container: appsettings.Development.json and its local connection string
        // are not loaded, so the test controls the connection string completely.
        builder.UseEnvironment(Environments.Production);

        // UseSetting, not ConfigureAppConfiguration: Program.cs reads the connection string before
        // configuration callbacks run. It also wins over a ConnectionStrings__Postgres variable in the
        // developer's shell, so an empty value reliably means "missing".
        builder.UseSetting(ConnectionStringKey, connectionString);
    }
}
