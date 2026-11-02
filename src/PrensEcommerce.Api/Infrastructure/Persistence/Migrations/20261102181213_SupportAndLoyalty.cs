using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrensEcommerce.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportAndLoyalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_loyalty_transactions_account_id",
                schema: "crm",
                table: "loyalty_transactions");

            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "support",
                table: "tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<int>(
                name: "number",
                schema: "support",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Numera los tickets que ya existieran (correlativo por tienda) antes del índice único.
            migrationBuilder.Sql("""
                UPDATE support.tickets t SET number = s.rn
                FROM (SELECT id, ROW_NUMBER() OVER (PARTITION BY tenant_id ORDER BY created_at) AS rn
                      FROM support.tickets) s
                WHERE t.id = s.id;
                """);

            migrationBuilder.AddColumn<string>(
                name: "satisfaction_comment",
                schema: "support",
                table: "tickets",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_discount",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "lifetime_points",
                schema: "crm",
                table: "loyalty_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Las cuentas existentes parten con el histórico igual a su saldo actual.
            migrationBuilder.Sql("UPDATE crm.loyalty_accounts SET lifetime_points = GREATEST(points_balance, 0);");

            migrationBuilder.CreateTable(
                name: "loyalty_vouchers",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loyalty_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_loyalty_vouchers_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "sales",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_loyalty_vouchers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "core",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loyalty_vouchers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_customer_id",
                schema: "support",
                table: "tickets",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_number",
                schema: "support",
                table: "tickets",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_account_id_created_at",
                schema: "crm",
                table: "loyalty_transactions",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_vouchers_order_id",
                schema: "crm",
                table: "loyalty_vouchers",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_vouchers_tenant_id",
                schema: "crm",
                table: "loyalty_vouchers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_vouchers_tenant_id_code",
                schema: "crm",
                table: "loyalty_vouchers",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_vouchers_tenant_id_user_id",
                schema: "crm",
                table: "loyalty_vouchers",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_vouchers_user_id",
                schema: "crm",
                table: "loyalty_vouchers",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loyalty_vouchers",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id_customer_id",
                schema: "support",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id_number",
                schema: "support",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_loyalty_transactions_account_id_created_at",
                schema: "crm",
                table: "loyalty_transactions");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "support",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "support",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "satisfaction_comment",
                schema: "support",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "loyalty_discount",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "lifetime_points",
                schema: "crm",
                table: "loyalty_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_account_id",
                schema: "crm",
                table: "loyalty_transactions",
                column: "account_id");
        }
    }
}
