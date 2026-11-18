using Freitty.Application.Common;
using Freitty.Application.Me.GetMe;
using Freitty.Domain.Companies;

namespace Freitty.Api.IntegrationTests.Me;

public sealed class GetMeTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task MeIsTheCurrentUserAndTheirCompany()
    {
        await api.SaveReferenceDataAsync();

        MeDto me = await api.GetAsync<MeDto>(ApiUrls.Me);

        Assert.Equal(
            new MeDto(
                new UserDto("User 1", "U1", UserRole.Admin),
                new CompanyDto(api.Data.Company.Name, 1250.50m),
                NeedAttentionCount: 0),
            me);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
