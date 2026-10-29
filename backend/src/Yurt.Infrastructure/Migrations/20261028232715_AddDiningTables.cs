using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiningTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DiningTableId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableLabel",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiningTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiningTables_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiningTables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiningTableId",
                table: "Orders",
                column: "DiningTableId");

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_Code",
                table: "DiningTables",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_LocationId_Label",
                table: "DiningTables",
                columns: new[] { "LocationId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_TenantId",
                table: "DiningTables",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DiningTables_DiningTableId",
                table: "Orders",
                column: "DiningTableId",
                principalTable: "DiningTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DiningTables_DiningTableId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "DiningTables");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DiningTableId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiningTableId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TableLabel",
                table: "Orders");
        }
    }
}
