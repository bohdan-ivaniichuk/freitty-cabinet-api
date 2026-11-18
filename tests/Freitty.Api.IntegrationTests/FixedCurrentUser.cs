using Freitty.Application.Abstractions;

namespace Freitty.Api.IntegrationTests;

internal sealed class FixedCurrentUser(CurrentUser user) : ICurrentUser
{
    public ValueTask<CurrentUser> GetAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(user);
    }
}
