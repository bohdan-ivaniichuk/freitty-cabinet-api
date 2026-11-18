using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freitty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PipelineIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_orders_company_id_status_scheduled_at_id",
                table: "orders",
                columns: new[] { "company_id", "status", "scheduled_at", "id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_company_id_status_scheduled_at_id",
                table: "orders");
        }
    }
}
