using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToppingGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "MenuToppings",
                type: "text",
                nullable: true);

            // Backfill groups for already-seeded toppings
            migrationBuilder.Sql("""
                UPDATE "MenuToppings"
                SET "Group" = 'milk'
                WHERE "Name" IN ('Oat Milk', 'Almond Milk', 'Soy Milk');
                """);
            migrationBuilder.Sql("""
                UPDATE "MenuToppings"
                SET "Group" = 'syrup'
                WHERE "Name" IN ('Vanilla Syrup', 'Caramel Syrup', 'Hazelnut Syrup', 'Brown Sugar Syrup');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                table: "MenuToppings");
        }
    }
}
