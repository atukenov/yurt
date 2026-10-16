using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToOrdersAndPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Promotions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PaymentWebhookLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OrderItemToppings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OrderItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GroupCarts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GroupCartMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GroupCartItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Favorites",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_TenantId",
                table: "Promotions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookLogs_TenantId",
                table: "PaymentWebhookLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId",
                table: "Payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemToppings_TenantId",
                table: "OrderItemToppings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCarts_TenantId",
                table: "GroupCarts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCartMembers_TenantId",
                table: "GroupCartMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCartItems_TenantId",
                table: "GroupCartItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_TenantId",
                table: "Favorites",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Tenants_TenantId",
                table: "Favorites",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupCartItems_Tenants_TenantId",
                table: "GroupCartItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupCartMembers_Tenants_TenantId",
                table: "GroupCartMembers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupCarts_Tenants_TenantId",
                table: "GroupCarts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Tenants_TenantId",
                table: "OrderItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItemToppings_Tenants_TenantId",
                table: "OrderItemToppings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tenants_TenantId",
                table: "Orders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentWebhookLogs_Tenants_TenantId",
                table: "PaymentWebhookLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Tenants_TenantId",
                table: "Promotions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Tenants_TenantId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupCartItems_Tenants_TenantId",
                table: "GroupCartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupCartMembers_Tenants_TenantId",
                table: "GroupCartMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupCarts_Tenants_TenantId",
                table: "GroupCarts");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Tenants_TenantId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItemToppings_Tenants_TenantId",
                table: "OrderItemToppings");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tenants_TenantId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentWebhookLogs_Tenants_TenantId",
                table: "PaymentWebhookLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Tenants_TenantId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_TenantId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentWebhookLogs_TenantId",
                table: "PaymentWebhookLogs");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItemToppings_TenantId",
                table: "OrderItemToppings");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_TenantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_GroupCarts_TenantId",
                table: "GroupCarts");

            migrationBuilder.DropIndex(
                name: "IX_GroupCartMembers_TenantId",
                table: "GroupCartMembers");

            migrationBuilder.DropIndex(
                name: "IX_GroupCartItems_TenantId",
                table: "GroupCartItems");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_TenantId",
                table: "Favorites");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PaymentWebhookLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrderItemToppings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GroupCarts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GroupCartMembers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GroupCartItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Favorites");
        }
    }
}
