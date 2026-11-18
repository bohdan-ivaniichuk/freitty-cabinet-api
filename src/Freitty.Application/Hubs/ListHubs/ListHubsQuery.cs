using FastEndpoints;

namespace Freitty.Application.Hubs.ListHubs;

public sealed record ListHubsQuery : ICommand<IReadOnlyList<HubDto>>;
