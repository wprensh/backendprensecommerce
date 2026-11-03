using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrensEcommerce.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "images",
                schema: "catalog",
                table: "products",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            // La imagen única que ya tenían los productos pasa a ser la primera de la galería.
            migrationBuilder.Sql("UPDATE catalog.products SET images = ARRAY[image_url] WHERE image_url <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "images",
                schema: "catalog",
                table: "products");
        }
    }
}
