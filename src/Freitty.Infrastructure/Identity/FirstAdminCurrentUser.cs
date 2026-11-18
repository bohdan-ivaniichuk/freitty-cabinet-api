using Freitty.Application.Abstractions;
using Freitty.Domain.Companies;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Infrastructure.Identity;

// There is no authentication yet: every request acts as the first Admin by id — in the sample data, the only one.
// Scoped, so the user is looked up once per request.
internal sealed class FirstAdminCurrentUser(AppDbContext db) : ICurrentUser
{
    private CurrentUser? currentUser;

    public async ValueTask<CurrentUser> GetAsync(CancellationToken cancellationToken)
    {
        currentUser ??= await FindFirstAdminAsync(cancellationToken);
        return currentUser;
    }

    private async Task<CurrentUser> FindFirstAdminAsync(CancellationToken cancellationToken)
    {
        CurrentUser? firstAdmin = await db.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Admin)
            .OrderBy(user => user.Id)
            .Select(user => new CurrentUser(user.Id, user.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);

        return firstAdmin ?? throw new InvalidOperationException(
            "There is no Admin user to act as: the database holds no sample data. Start the API with " +
            "Seed:Enabled set to true (the Seed__Enabled environment variable) on an empty database to seed it.");
    }
}
