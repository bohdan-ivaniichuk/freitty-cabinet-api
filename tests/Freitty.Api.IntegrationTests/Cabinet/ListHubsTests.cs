using Freitty.Application.Hubs.ListHubs;

namespace Freitty.Api.IntegrationTests.Cabinet;

public sealed class ListHubsTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);

    // Other tests add hubs to the shared database, so only the test's own two are compared: "Markham …" first.
    [Fact]
    public async Task HubsAreListedInNameOrder()
    {
        await api.SaveAsync();

        HubDto[] hubs = await api.GetAsync<HubDto[]>("/api/v1/hubs");

        HubDto[] ownHubs = hubs
            .Where(hub => hub.Id == api.Data.Hub.Id || hub.Id == api.Data.OtherHub.Id)
            .ToArray();
        Assert.Equal(
            [
                new HubDto(api.Data.Hub.Id, api.Data.Hub.Name, "Mark"),
                new HubDto(api.Data.OtherHub.Id, api.Data.OtherHub.Name, "Tor"),
            ],
            ownHubs);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }
}
