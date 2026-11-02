using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrensEcommerce.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberAndCheckoutTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "order_number",
                schema: "sales",
                table: "orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_cost",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal",
                schema: "sales",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Pedidos creados antes de esta migración: número único y subtotal = total.
            migrationBuilder.Sql("""
                UPDATE sales.orders
                SET order_number = 'PR-' || to_char(created_at, 'YYYYMMDD') || '-' || upper(substr(replace(id::text, '-', ''), 25, 8)),
                    subtotal = total
                WHERE order_number = '';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_orders_tenant_id_order_number",
                schema: "sales",
                table: "orders",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_tenant_id_order_number",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "order_number",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "shipping_cost",
                schema: "sales",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "subtotal",
                schema: "sales",
                table: "orders");
        }
    }
}
