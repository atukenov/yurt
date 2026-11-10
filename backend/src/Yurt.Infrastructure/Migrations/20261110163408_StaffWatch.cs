using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StaffWatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Zone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SentToTelegram = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchAlerts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchAlerts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CameraNames = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchBoxes_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchBoxes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchCameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ZonesJson = table.Column<string>(type: "text", nullable: false),
                    Snapshot = table.Column<byte[]>(type: "bytea", nullable: true),
                    SnapshotWidth = table.Column<int>(type: "integer", nullable: false),
                    SnapshotHeight = table.Column<int>(type: "integer", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnapshotRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchCameras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchCameras_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchCameras_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchMinutes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Minute = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Zone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Seconds = table.Column<int>(type: "integer", nullable: false),
                    Occupied = table.Column<int>(type: "integer", nullable: false),
                    PeopleSum = table.Column<int>(type: "integer", nullable: false),
                    PeopleMax = table.Column<int>(type: "integer", nullable: false),
                    Phone = table.Column<int>(type: "integer", nullable: false),
                    Still = table.Column<int>(type: "integer", nullable: false),
                    Entered = table.Column<int>(type: "integer", nullable: false),
                    Left = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchMinutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchMinutes_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchMinutes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchReports_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchReports_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Open = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Close = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    TelegramChatId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchSettings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WatchShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Station = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Start = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    End = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchShifts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchShifts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchAlerts_LocationId_At",
                table: "WatchAlerts",
                columns: new[] { "LocationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchAlerts_TenantId",
                table: "WatchAlerts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchBoxes_KeyHash",
                table: "WatchBoxes",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchBoxes_LocationId",
                table: "WatchBoxes",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchBoxes_TenantId",
                table: "WatchBoxes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchBoxes_TenantId_LocationId",
                table: "WatchBoxes",
                columns: new[] { "TenantId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchCameras_LocationId_Name",
                table: "WatchCameras",
                columns: new[] { "LocationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchCameras_TenantId",
                table: "WatchCameras",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchMinutes_LocationId_Minute_Zone",
                table: "WatchMinutes",
                columns: new[] { "LocationId", "Minute", "Zone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchMinutes_TenantId",
                table: "WatchMinutes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchReports_LocationId_Day",
                table: "WatchReports",
                columns: new[] { "LocationId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchReports_TenantId",
                table: "WatchReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchSettings_LocationId",
                table: "WatchSettings",
                column: "LocationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchSettings_TenantId",
                table: "WatchSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchShifts_LocationId_SortOrder",
                table: "WatchShifts",
                columns: new[] { "LocationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchShifts_TenantId",
                table: "WatchShifts",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchAlerts");

            migrationBuilder.DropTable(
                name: "WatchBoxes");

            migrationBuilder.DropTable(
                name: "WatchCameras");

            migrationBuilder.DropTable(
                name: "WatchMinutes");

            migrationBuilder.DropTable(
                name: "WatchReports");

            migrationBuilder.DropTable(
                name: "WatchSettings");

            migrationBuilder.DropTable(
                name: "WatchShifts");
        }
    }
}
