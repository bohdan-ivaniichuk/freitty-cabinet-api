using System.Globalization;
using Freitty.Application.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Infrastructure.Orders;

// nextval is atomic under concurrent submits and is never rolled back, so two submits never get one number.
internal sealed class OrderNumbers(AppDbContext db) : IOrderNumbers
{
    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        long sequenceValue = await db.Database
            .SqlQueryRaw<long>(OrderNumberSequence.NextValueSql)
            .SingleAsync(cancellationToken);
        return string.Format(CultureInfo.InvariantCulture, OrderNumberSequence.NumberFormat, sequenceValue);
    }
}
