using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchLiveAndAlertPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Picture",
                table: "WatchAlerts",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WatchLive",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchLive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchLive_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchLive_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // alerts the box sent twice (an answer got lost) are stored once from now on: drop old copies first
            migrationBuilder.Sql(@"DELETE FROM ""WatchAlerts"" a USING ""WatchAlerts"" b
WHERE a.""LocationId"" = b.""LocationId"" AND a.""At"" = b.""At"" AND a.""Kind"" = b.""Kind"" AND a.""Zone"" = b.""Zone""
  AND a.""Id"" > b.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_WatchAlerts_LocationId_At_Kind_Zone",
                table: "WatchAlerts",
                columns: new[] { "LocationId", "At", "Kind", "Zone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchLive_LocationId",
                table: "WatchLive",
                column: "LocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchLive_TenantId",
                table: "WatchLive",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchLive");

            migrationBuilder.DropIndex(
                name: "IX_WatchAlerts_LocationId_At_Kind_Zone",
                table: "WatchAlerts");

            migrationBuilder.DropColumn(
                name: "Picture",
                table: "WatchAlerts");
        }
    }
}
