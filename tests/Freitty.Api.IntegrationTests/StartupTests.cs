namespace Freitty.Api.IntegrationTests;

public sealed class StartupTests(PostgresFixture postgres)
{
    [Fact]
    public void StartupFailsNamingTheEnvironmentVariableWhenConnectionStringIsMissing()
    {
        using var factory = new ApiFactory(connectionString: string.Empty);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings__Postgres", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Eastern Standard Time")]
    [InlineData("Mars/Olympus_Mons")]
    public void StartupFailsNamingTheEnvironmentVariableWhenReportingTimeZoneIsNotAnIanaId(string timeZone)
    {
        using var factory = new ApiFactory(postgres.ConnectionString, reportingTimeZone: timeZone);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Cabinet__TimeZone", exception.Message);
    }
}
