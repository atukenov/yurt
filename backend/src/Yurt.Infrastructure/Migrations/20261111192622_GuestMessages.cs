using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "DiscountCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerUserId",
                table: "DiscountCodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingOptOut",
                table: "CustomerUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "CustomerDeviceTokens",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketingCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Segment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TitleRu = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TitleKk = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyRu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BodyKk = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CodeValidDays = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketingCampaigns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketingMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("22222222-0000-0000-0000-000000000001")),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketingMessages_CustomerUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "CustomerUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketingMessages_DiscountCodes_DiscountCodeId",
                        column: x => x.DiscountCodeId,
                        principalTable: "DiscountCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketingMessages_MarketingCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "MarketingCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketingMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_CampaignId",
                table: "DiscountCodes",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_CustomerUserId",
                table: "DiscountCodes",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaigns_TenantId",
                table: "MarketingCampaigns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingMessages_CampaignId_CustomerUserId",
                table: "MarketingMessages",
                columns: new[] { "CampaignId", "CustomerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingMessages_CustomerUserId_CreatedAt",
                table: "MarketingMessages",
                columns: new[] { "CustomerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingMessages_DiscountCodeId",
                table: "MarketingMessages",
                column: "DiscountCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingMessages_TenantId",
                table: "MarketingMessages",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountCodes_CustomerUsers_CustomerUserId",
                table: "DiscountCodes",
                column: "CustomerUserId",
                principalTable: "CustomerUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountCodes_MarketingCampaigns_CampaignId",
                table: "DiscountCodes",
                column: "CampaignId",
                principalTable: "MarketingCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscountCodes_CustomerUsers_CustomerUserId",
                table: "DiscountCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscountCodes_MarketingCampaigns_CampaignId",
                table: "DiscountCodes");

            migrationBuilder.DropTable(
                name: "MarketingMessages");

            migrationBuilder.DropTable(
                name: "MarketingCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_DiscountCodes_CampaignId",
                table: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_DiscountCodes_CustomerUserId",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "MarketingOptOut",
                table: "CustomerUsers");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "CustomerDeviceTokens");
        }
    }
}
