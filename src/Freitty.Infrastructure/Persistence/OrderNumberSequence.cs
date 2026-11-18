using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence;

// The sequence behind the official order numbers: FR followed by its value in six digits, or more once it outgrows
// them. It is not owned by a column, so TRUNCATE leaves it alone; whoever stores official numbers without it — the
// seed — sets it after them.
internal static class OrderNumberSequence
{
    public const string Name = "order_number_seq";
    public const string NumberPrefix = "FR";
    public const string NumberFormat = NumberPrefix + "{0:D6}";

    // A scalar query that EF Core composes over reads its column as "Value".
    public const string NextValueSql = $"SELECT nextval('{Name}') AS \"Value\"";

    // To the highest numeric part of the official numbers stored, so the next one follows it; with none, back to the
    // sequence's start. Draft numbers, and numbers of another shape, are not numbers of this sequence.
    public static readonly string ContinueAfterStoredNumbersSql = $"""
        SELECT setval(
            '{Name}',
            coalesce(max(substring(number FROM {NumberPrefix.Length + 1})::bigint), 1),
            max(number) IS NOT NULL)
        FROM orders
        WHERE status <> {(int)OrderStatus.Draft} AND number ~ '^{NumberPrefix}[0-9]+$'
        """;
}
