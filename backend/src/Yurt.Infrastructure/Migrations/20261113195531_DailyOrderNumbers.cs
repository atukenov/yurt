using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DailyOrderNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyNumber",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfflineNumber",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderDayCounters",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDayCounters", x => new { x.LocationId, x.Day });
                    table.ForeignKey(
                        name: "FK_OrderDayCounters_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDayCounters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Give orders that already exist a day number too, in the order they were placed: 1, 2, 3 ... per branch and
            // per day of the branch's own clock. Then start each day's counter at the last number given, so new orders
            // carry on from there. Safe to run on a live database: it only fills the new empty columns.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT o."Id",
                           (o."CreatedAt" AT TIME ZONE COALESCE(
                               (SELECT z.name FROM pg_timezone_names z WHERE z.name = l."TimeZone" LIMIT 1), 'Asia/Almaty'))::date AS day,
                           ROW_NUMBER() OVER (
                               PARTITION BY o."LocationId",
                                            (o."CreatedAt" AT TIME ZONE COALESCE(
                                                (SELECT z.name FROM pg_timezone_names z WHERE z.name = l."TimeZone" LIMIT 1), 'Asia/Almaty'))::date
                               ORDER BY o."CreatedAt", o."Id") AS n
                    FROM "Orders" o JOIN "Locations" l ON l."Id" = o."LocationId")
                UPDATE "Orders" o SET "BusinessDate" = numbered.day, "DailyNumber" = numbered.n
                FROM numbered WHERE numbered."Id" = o."Id";
                """);
            migrationBuilder.Sql("""
                INSERT INTO "OrderDayCounters" ("LocationId", "Day", "TenantId", "LastNumber")
                SELECT o."LocationId", o."BusinessDate", l."TenantId", MAX(o."DailyNumber")
                FROM "Orders" o JOIN "Locations" l ON l."Id" = o."LocationId"
                WHERE o."DailyNumber" IS NOT NULL GROUP BY o."LocationId", o."BusinessDate", l."TenantId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_LocationId_BusinessDate_DailyNumber",
                table: "Orders",
                columns: new[] { "LocationId", "BusinessDate", "DailyNumber" },
                unique: true,
                filter: "\"DailyNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDayCounters_TenantId",
                table: "OrderDayCounters",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDayCounters");

            migrationBuilder.DropIndex(
                name: "IX_Orders_LocationId_BusinessDate_DailyNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DailyNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OfflineNumber",
                table: "Orders");
        }
    }
}
