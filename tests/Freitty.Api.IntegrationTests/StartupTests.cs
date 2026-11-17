namespace Freitty.Api.IntegrationTests;

public sealed class StartupTests
{
    [Fact]
    public void StartupFailsNamingTheEnvironmentVariableWhenConnectionStringIsMissing()
    {
        using var factory = new ApiFactory(connectionString: string.Empty);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings__Postgres", exception.Message);
    }
}
