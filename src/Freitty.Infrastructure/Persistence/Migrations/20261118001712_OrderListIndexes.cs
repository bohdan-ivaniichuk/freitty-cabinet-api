using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freitty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_ref_number_trgm",
                table: "shipments",
                column: "ref_number")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_company_id_scheduled_at_id",
                table: "orders",
                columns: new[] { "company_id", "scheduled_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_orders_number_trgm",
                table: "orders",
                column: "number")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shipments_ref_number_trgm",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "ix_orders_company_id_scheduled_at_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_number_trgm",
                table: "orders");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
