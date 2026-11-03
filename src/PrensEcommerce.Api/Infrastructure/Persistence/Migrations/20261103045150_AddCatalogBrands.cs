using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrensEcommerce.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "brand_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "brands",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brands", x => x.id);
                    table.ForeignKey(
                        name: "fk_brands_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "core",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_brand_id",
                schema: "catalog",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "ix_brands_tenant_id",
                schema: "catalog",
                table: "brands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_brands_tenant_id_slug",
                schema: "catalog",
                table: "brands",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_products_brands_brand_id",
                schema: "catalog",
                table: "products",
                column: "brand_id",
                principalSchema: "catalog",
                principalTable: "brands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_products_brands_brand_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropTable(
                name: "brands",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_products_brand_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "brand_id",
                schema: "catalog",
                table: "products");
        }
    }
}
