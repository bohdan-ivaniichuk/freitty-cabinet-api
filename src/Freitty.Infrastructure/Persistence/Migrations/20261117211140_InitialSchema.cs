using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Freitty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carriers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carriers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hubs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    short_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    region = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hubs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_services", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "supply_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supply_items", x => x.id);
                    table.CheckConstraint("ck_supply_items_unit_price", "unit_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_users_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    cross_dock_kind = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_hub_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    destination_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    destination_region = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    transport_kind = table.Column<int>(type: "integer", nullable: true),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    driver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.CheckConstraint("ck_orders_carrier_matches_transport", "(transport_kind IS NOT DISTINCT FROM 1) = (carrier_id IS NOT NULL)");
                    table.CheckConstraint("ck_orders_completed_at_matches_status", "(status = 4) = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_orders_cross_dock_kind", "cross_dock_kind IN (1, 2)");
                    table.CheckConstraint("ck_orders_cross_dock_kind_matches_type", "(type = 1) = (cross_dock_kind IS NOT NULL)");
                    table.CheckConstraint("ck_orders_destination_complete", "(destination_city IS NULL) = (destination_region IS NULL)");
                    table.CheckConstraint("ck_orders_driver_matches_transport", "(transport_kind IS NOT DISTINCT FROM 2) = (driver_name IS NOT NULL)");
                    table.CheckConstraint("ck_orders_milestones_in_order", "created_at <= submitted_at AND submitted_at <= started_at AND started_at <= completed_at");
                    table.CheckConstraint("ck_orders_started_at_matches_status", "(status IN (3, 4)) = (started_at IS NOT NULL)");
                    table.CheckConstraint("ck_orders_status", "status IN (1, 2, 3, 4)");
                    table.CheckConstraint("ck_orders_submitted_at_matches_status", "(status <> 1) = (submitted_at IS NOT NULL)");
                    table.CheckConstraint("ck_orders_submitted_has_details", "status = 1 OR (scheduled_at IS NOT NULL AND destination_city IS NOT NULL AND transport_kind IS NOT NULL)");
                    table.CheckConstraint("ck_orders_transfer_hub_differs", "transfer_hub_id <> hub_id");
                    table.CheckConstraint("ck_orders_transport_kind", "transport_kind IN (1, 2, 3)");
                    table.CheckConstraint("ck_orders_type", "type IN (1, 2)");
                    table.CheckConstraint("ck_orders_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_orders_carriers_carrier_id",
                        column: x => x.carrier_id,
                        principalTable: "carriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_hubs_hub_id",
                        column: x => x.hub_id,
                        principalTable: "hubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_hubs_transfer_hub_id",
                        column: x => x.transfer_hub_id,
                        principalTable: "hubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.CheckConstraint("ck_invoices_amount", "amount >= 0");
                    table.CheckConstraint("ck_invoices_paid_after_issued", "paid_at >= issued_at");
                    table.ForeignKey(
                        name: "fk_invoices_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_services", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_services_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_services_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    ref_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    warehouse_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    trailer_type = table.Column<int>(type: "integer", nullable: true),
                    truck_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    trailer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    arrived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dock = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    floor_lead = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dock_assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipments", x => x.id);
                    table.CheckConstraint("ck_shipments_arrival_complete", "num_nulls(trailer_type, truck_number, trailer_number, arrived_at) IN (0, 4)");
                    table.CheckConstraint("ck_shipments_dock_complete", "num_nulls(dock, bay, floor_lead, dock_assigned_at) IN (0, 4)");
                    table.CheckConstraint("ck_shipments_position", "position >= 1");
                    table.CheckConstraint("ck_shipments_trailer_type", "trailer_type IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "fk_shipments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supply_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supply_lines", x => x.id);
                    table.CheckConstraint("ck_supply_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_supply_lines_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_supply_lines_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_supply_lines_supply_items_supply_item_id",
                        column: x => x.supply_item_id,
                        principalTable: "supply_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => x.id);
                    table.CheckConstraint("ck_alerts_resolved_after_raised", "resolved_at >= raised_at");
                    table.CheckConstraint("ck_alerts_type", "type IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_alerts_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cargo_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargo_lines", x => x.id);
                    table.CheckConstraint("ck_cargo_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_cargo_lines_unit_type", "unit_type IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_cargo_lines_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    unit_type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    trailer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_billable = table.Column<bool>(type: "boolean", nullable: false),
                    photo_count = table.Column<int>(type: "integer", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operations", x => x.id);
                    table.CheckConstraint("ck_operations_comment_count", "comment_count >= 0");
                    table.CheckConstraint("ck_operations_photo_count", "photo_count >= 0");
                    table.CheckConstraint("ck_operations_quantity", "quantity > 0");
                    table.CheckConstraint("ck_operations_trailer_number_matches_type", "(type IN (1, 4)) = (trailer_number IS NOT NULL)");
                    table.CheckConstraint("ck_operations_type", "type IN (1, 2, 3, 4)");
                    table.CheckConstraint("ck_operations_unit_type", "unit_type IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_operations_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_shipment_id",
                table: "alerts",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_shipment_id_type",
                table: "alerts",
                columns: new[] { "shipment_id", "type" },
                unique: true,
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cargo_lines_shipment_id_unit_type",
                table: "cargo_lines",
                columns: new[] { "shipment_id", "unit_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carriers_name",
                table: "carriers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hubs_name",
                table: "hubs",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_number",
                table: "invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_order_id",
                table: "invoices",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_operations_shipment_id",
                table: "operations",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_services_order_id_service_id",
                table: "order_services",
                columns: new[] { "order_id", "service_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_services_service_id",
                table: "order_services",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_carrier_id",
                table: "orders",
                column: "carrier_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_company_id_number",
                table: "orders",
                columns: new[] { "company_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_by_id",
                table: "orders",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_hub_id",
                table: "orders",
                column: "hub_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_number",
                table: "orders",
                column: "number",
                unique: true,
                filter: "status <> 1");

            migrationBuilder.CreateIndex(
                name: "ix_orders_transfer_hub_id",
                table: "orders",
                column: "transfer_hub_id");

            migrationBuilder.CreateIndex(
                name: "ix_services_name",
                table: "services",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_order_id_position",
                table: "shipments",
                columns: new[] { "order_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_order_id_ref_number",
                table: "shipments",
                columns: new[] { "order_id", "ref_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supply_items_sku",
                table: "supply_items",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supply_lines_order_id",
                table: "supply_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_supply_lines_supply_item_id",
                table: "supply_lines",
                column: "supply_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_company_id",
                table: "users",
                column: "company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "cargo_lines");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "operations");

            migrationBuilder.DropTable(
                name: "order_services");

            migrationBuilder.DropTable(
                name: "supply_lines");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "services");

            migrationBuilder.DropTable(
                name: "supply_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "carriers");

            migrationBuilder.DropTable(
                name: "hubs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}
