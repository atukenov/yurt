using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "MenuItemLocations",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenuItemToppings",
                columns: table => new
                {
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToppingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001"))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemToppings", x => new { x.MenuItemId, x.ToppingId });
                    table.ForeignKey(
                        name: "FK_MenuItemToppings_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemToppings_MenuToppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "MenuToppings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemToppings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuOptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TitleRu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TitleKk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    MinSelect = table.Column<int>(type: "integer", nullable: false),
                    MaxSelect = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuOptionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuOptionGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuToppingLocations",
                columns: table => new
                {
                    ToppingId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuToppingLocations", x => new { x.ToppingId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_MenuToppingLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuToppingLocations_MenuToppings_ToppingId",
                        column: x => x.ToppingId,
                        principalTable: "MenuToppings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuToppingLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemToppings_TenantId",
                table: "MenuItemToppings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemToppings_ToppingId",
                table: "MenuItemToppings",
                column: "ToppingId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOptionGroups_TenantId",
                table: "MenuOptionGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuOptionGroups_TenantId_Key",
                table: "MenuOptionGroups",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuToppingLocations_LocationId",
                table: "MenuToppingLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuToppingLocations_TenantId",
                table: "MenuToppingLocations",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuItemToppings");

            migrationBuilder.DropTable(
                name: "MenuOptionGroups");

            migrationBuilder.DropTable(
                name: "MenuToppingLocations");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "MenuItemLocations");
        }
    }
}
