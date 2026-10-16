using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToMenuAndLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new TenantId columns default to the default tenant, so that row must exist before the foreign keys are added.
            migrationBuilder.Sql(
                "INSERT INTO \"Tenants\" (\"Id\", \"Slug\", \"Name\", \"IsActive\", \"CreatedAt\") " +
                "VALUES ('22222222-0000-0000-0000-000000000001', 'default', 'Yurt', true, now()) ON CONFLICT (\"Id\") DO NOTHING;");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuToppings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuToppingCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuItemVariants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuItemLocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MenuCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Locations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_MenuToppings_TenantId",
                table: "MenuToppings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuToppingCategories_TenantId",
                table: "MenuToppingCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemVariants_TenantId",
                table: "MenuItemVariants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_TenantId",
                table: "MenuItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemLocations_TenantId",
                table: "MenuItemLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_TenantId",
                table: "MenuCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_TenantId",
                table: "Locations",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                table: "Locations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Tenants_TenantId",
                table: "MenuCategories",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemLocations_Tenants_TenantId",
                table: "MenuItemLocations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItemVariants_Tenants_TenantId",
                table: "MenuItemVariants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuToppingCategories_Tenants_TenantId",
                table: "MenuToppingCategories",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuToppings_Tenants_TenantId",
                table: "MenuToppings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Tenants_TenantId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemLocations_Tenants_TenantId",
                table: "MenuItemLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Tenants_TenantId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItemVariants_Tenants_TenantId",
                table: "MenuItemVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuToppingCategories_Tenants_TenantId",
                table: "MenuToppingCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuToppings_Tenants_TenantId",
                table: "MenuToppings");

            migrationBuilder.DropIndex(
                name: "IX_MenuToppings_TenantId",
                table: "MenuToppings");

            migrationBuilder.DropIndex(
                name: "IX_MenuToppingCategories_TenantId",
                table: "MenuToppingCategories");

            migrationBuilder.DropIndex(
                name: "IX_MenuItemVariants_TenantId",
                table: "MenuItemVariants");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_TenantId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItemLocations_TenantId",
                table: "MenuItemLocations");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_TenantId",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_Locations_TenantId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuToppings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuToppingCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItemVariants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuItemLocations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Locations");
        }
    }
}
