using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToUsersAndAdminData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_MobileNumber_Purpose",
                table: "PhoneVerifications");

            migrationBuilder.DropIndex(
                name: "IX_DiscountCodes_Code",
                table: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_CustomerUsers_MobileNumber",
                table: "CustomerUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppReleases_Version",
                table: "AppReleases");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PhoneVerifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DiscountCodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomerUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CustomerDeviceTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppReleases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AdminUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_TenantId",
                table: "UserReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_TenantId",
                table: "PhoneVerifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_TenantId_MobileNumber_Purpose",
                table: "PhoneVerifications",
                columns: new[] { "TenantId", "MobileNumber", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_TenantId",
                table: "DiscountCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_TenantId_Code",
                table: "DiscountCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsers_TenantId",
                table: "CustomerUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsers_TenantId_MobileNumber",
                table: "CustomerUsers",
                columns: new[] { "TenantId", "MobileNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDeviceTokens_TenantId",
                table: "CustomerDeviceTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_TenantId",
                table: "AppReleases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_TenantId_Version",
                table: "AppReleases",
                columns: new[] { "TenantId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_TenantId",
                table: "AdminUsers",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminUsers_Tenants_TenantId",
                table: "AdminUsers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppReleases_Tenants_TenantId",
                table: "AppReleases",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerDeviceTokens_Tenants_TenantId",
                table: "CustomerDeviceTokens",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerUsers_Tenants_TenantId",
                table: "CustomerUsers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountCodes_Tenants_TenantId",
                table: "DiscountCodes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PhoneVerifications_Tenants_TenantId",
                table: "PhoneVerifications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Tenants_TenantId",
                table: "RefreshTokens",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReports_Tenants_TenantId",
                table: "UserReports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminUsers_Tenants_TenantId",
                table: "AdminUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AppReleases_Tenants_TenantId",
                table: "AppReleases");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerDeviceTokens_Tenants_TenantId",
                table: "CustomerDeviceTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerUsers_Tenants_TenantId",
                table: "CustomerUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscountCodes_Tenants_TenantId",
                table: "DiscountCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_PhoneVerifications_Tenants_TenantId",
                table: "PhoneVerifications");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Tenants_TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReports_Tenants_TenantId",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_TenantId",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_TenantId",
                table: "PhoneVerifications");

            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_TenantId_MobileNumber_Purpose",
                table: "PhoneVerifications");

            migrationBuilder.DropIndex(
                name: "IX_DiscountCodes_TenantId",
                table: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_DiscountCodes_TenantId_Code",
                table: "DiscountCodes");

            migrationBuilder.DropIndex(
                name: "IX_CustomerUsers_TenantId",
                table: "CustomerUsers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerUsers_TenantId_MobileNumber",
                table: "CustomerUsers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDeviceTokens_TenantId",
                table: "CustomerDeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AppReleases_TenantId",
                table: "AppReleases");

            migrationBuilder.DropIndex(
                name: "IX_AppReleases_TenantId_Version",
                table: "AppReleases");

            migrationBuilder.DropIndex(
                name: "IX_AdminUsers_TenantId",
                table: "AdminUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PhoneVerifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DiscountCodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomerUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomerDeviceTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppReleases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AdminUsers");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_MobileNumber_Purpose",
                table: "PhoneVerifications",
                columns: new[] { "MobileNumber", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_Code",
                table: "DiscountCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsers_MobileNumber",
                table: "CustomerUsers",
                column: "MobileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppReleases_Version",
                table: "AppReleases",
                column: "Version",
                unique: true);
        }
    }
}
