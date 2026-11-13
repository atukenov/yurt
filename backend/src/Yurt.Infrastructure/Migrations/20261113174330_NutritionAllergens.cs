using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NutritionAllergens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Allergens",
                table: "MenuItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Carbs",
                table: "MenuItems",
                type: "numeric(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fat",
                table: "MenuItems",
                type: "numeric(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Protein",
                table: "MenuItems",
                type: "numeric(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeightGrams",
                table: "MenuItems",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Allergens",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Calories",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Carbs",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Fat",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Protein",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "WeightGrams",
                table: "MenuItems");
        }
    }
}
