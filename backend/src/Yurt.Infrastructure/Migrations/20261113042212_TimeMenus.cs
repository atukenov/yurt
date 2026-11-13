using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TimeMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "MenuCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Locations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MenuSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    EndMinute = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuSchedules_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuTimePrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    PercentOff = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuTimePrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuTimePrices_MenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuTimePrices_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuTimePrices_MenuSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "MenuSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuTimePrices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ScheduleId",
                table: "MenuItems",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_ScheduleId",
                table: "MenuCategories",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_LocationId",
                table: "MenuSchedules",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuSchedules_TenantId",
                table: "MenuSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTimePrices_CategoryId",
                table: "MenuTimePrices",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTimePrices_MenuItemId",
                table: "MenuTimePrices",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTimePrices_ScheduleId",
                table: "MenuTimePrices",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuTimePrices_TenantId",
                table: "MenuTimePrices",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_MenuSchedules_ScheduleId",
                table: "MenuCategories",
                column: "ScheduleId",
                principalTable: "MenuSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_MenuSchedules_ScheduleId",
                table: "MenuItems",
                column: "ScheduleId",
                principalTable: "MenuSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_MenuSchedules_ScheduleId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_MenuSchedules_ScheduleId",
                table: "MenuItems");

            migrationBuilder.DropTable(
                name: "MenuTimePrices");

            migrationBuilder.DropTable(
                name: "MenuSchedules");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_ScheduleId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_ScheduleId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Locations");
        }
    }
}
