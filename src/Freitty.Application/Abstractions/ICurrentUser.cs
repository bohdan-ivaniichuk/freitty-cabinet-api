namespace Freitty.Application.Abstractions;

// The user the request acts as. Every query is scoped by the company of this user, so authentication can
// replace the implementation without touching a query.
public interface ICurrentUser
{
    ValueTask<CurrentUser> GetAsync(CancellationToken cancellationToken);
}
