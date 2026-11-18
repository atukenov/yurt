using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BarcodesAndWeighedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeightGrams",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "MenuItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SoldByWeight",
                table: "MenuItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Ingredients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId_Barcode",
                table: "MenuItems",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_TenantId_Barcode",
                table: "Ingredients",
                columns: new[] { "TenantId", "Barcode" },
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId_Barcode",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_TenantId_Barcode",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "WeightGrams",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "SoldByWeight",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Ingredients");
        }
    }
}
