using FastEndpoints;
using Freitty.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Hubs.ListHubs;

// Hubs belong to the platform, not to a company: every client sees all of them.
public sealed class ListHubsQueryHandler(IAppDbContext db) : ICommandHandler<ListHubsQuery, IReadOnlyList<HubDto>>
{
    public async Task<IReadOnlyList<HubDto>> ExecuteAsync(ListHubsQuery query, CancellationToken ct)
    {
        return await db.Hubs
            .AsNoTracking()
            .OrderBy(hub => hub.Name)
            .Select(hub => new HubDto(hub.Id, hub.Name, hub.ShortName))
            .ToListAsync(ct);
    }
}
