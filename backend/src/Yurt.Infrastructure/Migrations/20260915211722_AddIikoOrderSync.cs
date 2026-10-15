using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIikoOrderSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IikoDeliveryOrderId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IikoOrderSyncPendingAction",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IikoOrderSyncStatus",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "IikoProductSizeId",
                table: "MenuItemVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IikoProductId",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IikoProductSizeId",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IikoTerminalGroupId",
                table: "Locations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IikoDeliveryOrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IikoOrderSyncPendingAction",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IikoOrderSyncStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IikoProductSizeId",
                table: "MenuItemVariants");

            migrationBuilder.DropColumn(
                name: "IikoProductId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IikoProductSizeId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IikoTerminalGroupId",
                table: "Locations");
        }
    }
}
