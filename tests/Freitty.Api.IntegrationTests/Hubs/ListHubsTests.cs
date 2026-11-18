using Freitty.Application.Hubs.ListHubs;
using Freitty.Domain.Hubs;

namespace Freitty.Api.IntegrationTests.Hubs;

public sealed class ListHubsTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    // Other tests add hubs to the shared database, so only the test's own three are compared. The one saved last
    // sorts first, so neither the order of creation nor that of the ids can pass for the order of names.
    [Fact]
    public async Task HubsAreListedInNameOrder()
    {
        var savedLast = new Hub(TestData.Unique("Ajax "), "Ajax", "ON", "America/Toronto");
        await api.SaveReferenceDataAsync();
        await api.SaveAsync(savedLast);

        HubDto[] hubs = await api.GetAsync<HubDto[]>(ApiUrls.Hubs);

        Guid[] ownHubIds = [api.Data.Hub.Id, api.Data.OtherHub.Id, savedLast.Id];
        Assert.Equal(
            [
                new HubDto(savedLast.Id, savedLast.Name, "Ajax"),
                new HubDto(api.Data.Hub.Id, api.Data.Hub.Name, "Mark"),
                new HubDto(api.Data.OtherHub.Id, api.Data.OtherHub.Name, "Tor"),
            ],
            hubs.Where(hub => ownHubIds.Contains(hub.Id)));
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
