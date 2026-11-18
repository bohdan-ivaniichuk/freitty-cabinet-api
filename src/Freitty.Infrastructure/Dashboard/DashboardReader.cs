using Dapper;
using Freitty.Application.Common;
using Freitty.Application.Dashboard;
using Freitty.Application.Dashboard.GetDashboard;
using Freitty.Domain.Orders;
using Npgsql;

namespace Freitty.Infrastructure.Dashboard;

// Each method borrows a connection of its own from the data source and returns it, so the reader never shares the
// connection of the database context. Instants travel in UTC; the zone is the reporting zone's IANA id.
internal sealed class DashboardReader(NpgsqlDataSource dataSource, ReportingTimeZone reportingTimeZone)
    : IDashboardReader
{
    // The bounds are computed here: `@Now - interval '30 days'` would count days in the session's time zone, which
    // the connection does not fix.
    public async Task<DashboardFigures> ReadFiguresAsync(
        Guid companyId,
        DateTimeOffset now,
        TimeSpan rollingWindow,
        CancellationToken cancellationToken)
    {
        DateTimeOffset utcNow = now.ToUniversalTime();
        var parameters = new
        {
            CompanyId = companyId,
            Now = utcNow,
            Last30DaysFrom = utcNow - rollingWindow,
            Previous30DaysFrom = utcNow - (2 * rollingWindow),
            Zone = reportingTimeZone.Id,
            ActiveStatuses = ActiveOrderStatuses.All.Select(status => (int)status).ToArray(),
        };
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<DashboardFigures>(
            Command(DashboardSql.Figures, parameters, cancellationToken));
    }

    public async Task<IReadOnlyList<ActivityBucketDto>> ReadActivityAsync(
        Guid companyId,
        DateTimeOffset now,
        ActivityAxis axis,
        CancellationToken cancellationToken)
    {
        var parameters = new
        {
            CompanyId = companyId,
            Now = now.ToUniversalTime(),
            Zone = reportingTimeZone.Id,
            axis.Unit,
            axis.Step,
            axis.Buckets,
        };
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<ActivityBucketDto> buckets = await connection.QueryAsync<ActivityBucketDto>(
            Command(DashboardSql.Activity, parameters, cancellationToken));
        return buckets.AsList();
    }

    public async Task<AttentionCounts> ReadAttentionCountsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var parameters = new { CompanyId = companyId, DraftStatus = (int)OrderStatus.Draft };
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<AttentionCounts>(
            Command(DashboardSql.AttentionCounts, parameters, cancellationToken));
    }

    public async Task<IReadOnlyList<AlertOrderDto>> ReadAlertOrdersAsync(
        Guid companyId,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = new { CompanyId = companyId, Limit = limit };
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<AlertOrderRow> rows = await connection.QueryAsync<AlertOrderRow>(
            Command(DashboardSql.AlertOrders, parameters, cancellationToken));
        return rows
            .Select(row => new AlertOrderDto(row.Number, row.AlertTypes.Select(type => (AlertType)type).ToList()))
            .ToList();
    }

    private static CommandDefinition Command(string sql, object parameters, CancellationToken cancellationToken)
    {
        return new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
    }

    // Properties, not a positional record: Npgsql reports an array column as System.Array, which matches no
    // constructor parameter, while a property setter is given the int[].
    private sealed class AlertOrderRow
    {
        public string Number { get; init; } = string.Empty;

        public int[] AlertTypes { get; init; } = [];
    }
}
