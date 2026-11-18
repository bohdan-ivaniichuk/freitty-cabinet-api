using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freitty.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    private const int MoneyPrecision = 12;
    private const int MoneyScale = 2;

    // One currency (CAD), so money is a plain numeric with no currency column.
    public static PropertyBuilder<decimal> IsMoney(this PropertyBuilder<decimal> property)
    {
        return property.HasPrecision(MoneyPrecision, MoneyScale);
    }

    // A GIN index of trigrams, which serves ILIKE '%…%' on the column.
    public static IndexBuilder<TEntity> IsTrigramIndex<TEntity>(this IndexBuilder<TEntity> index)
    {
        return index.HasMethod("gin").HasOperators("gin_trgm_ops");
    }

    // The list is built from the enum, so a new member changes the constraint in the next migration. The naming
    // convention does not touch raw SQL: the column is given in snake_case.
    public static TableBuilder<TEntity> HasEnumCheckConstraint<TEntity, TEnum>(
        this TableBuilder<TEntity> table,
        string column)
        where TEntity : class
        where TEnum : struct, Enum
    {
        string values = string.Join(
            ", ",
            Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        table.HasCheckConstraint($"ck_{table.Name}_{column}", $"{column} IN ({values})");
        return table;
    }
}
