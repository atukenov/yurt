using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModulePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModulePrices",
                table: "CafeBillings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PriceByHand",
                table: "CafeBillings",
                type: "boolean",
                nullable: false,
                // Plans saved before module prices existed had their price typed by hand.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModulePrices",
                table: "CafeBillings");

            migrationBuilder.DropColumn(
                name: "PriceByHand",
                table: "CafeBillings");
        }
    }
}
