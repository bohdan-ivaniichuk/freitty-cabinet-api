using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freitty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "order_number_seq");

            // The next official number follows the highest one already stored (status 1 is Draft). Written out
            // rather than taken from OrderNumberSequence, so this migration does the same whatever that class
            // becomes.
            migrationBuilder.Sql("""
                SELECT setval(
                    'order_number_seq',
                    coalesce(max(substring(number FROM 3)::bigint), 1),
                    max(number) IS NOT NULL)
                FROM orders
                WHERE status <> 1 AND number ~ '^FR[0-9]+$'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "order_number_seq");
        }
    }
}
