using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Common;
using Freitty.Application.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Me.GetMe;

public sealed class GetMeQueryHandler(IAppDbContext db, ICurrentUser currentUser, IDashboardReader dashboardReader)
    : ICommandHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> ExecuteAsync(GetMeQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        var me = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == user.UserId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.Role,
                Company = new CompanyDto(candidate.Company.Name, candidate.Company.Balance),
            })
            .SingleAsync(ct);
        AttentionCounts attention = await dashboardReader.ReadAttentionCountsAsync(user.CompanyId, ct);

        return new MeDto(UserDto.Of(me.Name, me.Role), me.Company, attention.Total);
    }
}
