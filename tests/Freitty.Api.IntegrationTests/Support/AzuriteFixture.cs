using Testcontainers.Azurite;

[assembly: AssemblyFixture(typeof(AzuriteFixture))]

namespace Freitty.Api.IntegrationTests.Support;

// One Azurite container per test run, the photo storage of every API the tests start. The image tag matches
// docker-compose.yml; change both together.
//
// Every ApiFactory reads the connection string from here, while the tests that start one know only their database:
// the storage is the same for all of them, so it is not passed through each. It is set before any test runs, as the
// runner initializes the assembly fixtures first.
public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/azure-storage/azurite:3.38.0";

    private static string? connectionString;

    // Azurite sends usage data unless told not to; a test run has no business doing that.
    private readonly AzuriteContainer container = new AzuriteBuilder(Image).WithCommand("--disableTelemetry").Build();

    public static string ConnectionString => connectionString
        ?? throw new InvalidOperationException("The Azurite container is started before the first test.");

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        connectionString = container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }
}
