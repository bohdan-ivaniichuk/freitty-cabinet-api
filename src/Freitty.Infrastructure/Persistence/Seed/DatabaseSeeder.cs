using Freitty.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Freitty.Infrastructure.Persistence.Seed;

// A database with a company is never touched: fresh sample data means an empty database. One SaveChangesAsync
// is one transaction, so a failure leaves the database empty, and of two instances seeding at once the unique
// hub name stops the second.
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

        logger.LogInformation(
            "Seeded sample data: {OrderCount} orders, anchor day {AnchorDay:yyyy-MM-dd}",
            scripts.Count,
            calendar.AnchorDay);
    }
}
