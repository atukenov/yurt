using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionKk",
                table: "Promotions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionRu",
                table: "Promotions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleKk",
                table: "Promotions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleRu",
                table: "Promotions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKk",
                table: "MenuToppings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "MenuToppings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionKk",
                table: "MenuItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionRu",
                table: "MenuItems",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKk",
                table: "MenuItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "MenuItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKk",
                table: "MenuCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "MenuCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKk",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "Locations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionKk",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "DescriptionRu",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TitleKk",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TitleRu",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "NameKk",
                table: "MenuToppings");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "MenuToppings");

            migrationBuilder.DropColumn(
                name: "DescriptionKk",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "DescriptionRu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameKk",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameKk",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "NameKk",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "Locations");
        }
    }
}
