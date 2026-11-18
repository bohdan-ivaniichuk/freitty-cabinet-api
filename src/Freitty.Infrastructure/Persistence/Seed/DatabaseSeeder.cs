using Freitty.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Freitty.Infrastructure.Persistence.Seed;

// Two ways in. Seeding fills an empty database and never touches one with a company: one SaveChangesAsync is one
// transaction, so a failure leaves the database empty, and of two instances seeding at once the unique hub name stops
// the second. Rebuilding, with Seed:Refresh on, replaces whatever is there in one transaction, so a failure leaves the
// previous data in place. It runs behind the closed request gate, so nothing of this process reads or writes meanwhile.
internal sealed class DatabaseSeeder(AppDbContext db, TimeProvider clock, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Companies.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Sample data skipped: the database is not empty");
            return;
        }

        var calendar = new SeedCalendar(clock, ReferenceData.HubTimeZone);
        int orderCount = await AddSampleDataAsync(calendar, cancellationToken);

        logger.LogInformation(
            "Seeded sample data: {OrderCount} orders, anchor day {AnchorDay:yyyy-MM-dd}",
            orderCount,
            calendar.AnchorDay);
    }

    // Returns the anchor day the new data is dated from.
    public async Task<DateOnly> RebuildAsync(CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await TruncateEveryTableAsync(cancellationToken);

        var calendar = new SeedCalendar(clock, ReferenceData.HubTimeZone);
        int orderCount = await AddSampleDataAsync(calendar, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Rebuilt sample data: {OrderCount} orders, anchor day {AnchorDay:yyyy-MM-dd}",
            orderCount,
            calendar.AnchorDay);
        return calendar.AnchorDay;
    }

    private async Task<int> AddSampleDataAsync(SeedCalendar calendar, CancellationToken cancellationToken)
    {
        var reference = new ReferenceData();
        List<OrderScript> scripts =
        [
            .. new ScriptedOrders(reference, calendar).Build(),
            .. new OrderHistory(reference, calendar).Build(),
        ];
        IReadOnlyList<Invoice> invoices = Invoices.Issue(scripts);

        db.AddRange(reference.Entities());
        db.AddRange(scripts.Select(script => script.Order));
        db.AddRange(invoices);
        await db.SaveChangesAsync(cancellationToken);
        return scripts.Count;
    }

    // One statement for every table of the model, so their foreign keys need no order; the migrations history is not
    // in the model and survives. TRUNCATE holds its lock until the commit: a reader of another process waits for the
    // new rows instead of seeing none. Identifiers cannot be parameters, so the names are quoted here.
    private async Task TruncateEveryTableAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string> tableNames = db.Model.GetRelationalModel().Tables.Select(QualifiedName);
        string truncate = $"TRUNCATE TABLE {string.Join(", ", tableNames)}";
        await db.Database.ExecuteSqlRawAsync(truncate, cancellationToken);
    }

    private static string QualifiedName(ITable table)
    {
        return table.Schema is null ? Quoted(table.Name) : $"{Quoted(table.Schema)}.{Quoted(table.Name)}";
    }

    private static string Quoted(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
