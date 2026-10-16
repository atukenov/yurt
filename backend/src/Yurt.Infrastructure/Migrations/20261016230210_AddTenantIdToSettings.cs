using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformSettings_Key",
                table: "PlatformSettings");

            migrationBuilder.DropIndex(
                name: "IX_ModuleLicenses_ModuleKey",
                table: "ModuleLicenses");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PlatformSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ModuleLicenses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_TenantId",
                table: "PlatformSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_TenantId_Key",
                table: "PlatformSettings",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLicenses_TenantId",
                table: "ModuleLicenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLicenses_TenantId_ModuleKey",
                table: "ModuleLicenses",
                columns: new[] { "TenantId", "ModuleKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ModuleLicenses_Tenants_TenantId",
                table: "ModuleLicenses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformSettings_Tenants_TenantId",
                table: "PlatformSettings",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModuleLicenses_Tenants_TenantId",
                table: "ModuleLicenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformSettings_Tenants_TenantId",
                table: "PlatformSettings");

            migrationBuilder.DropIndex(
                name: "IX_PlatformSettings_TenantId",
                table: "PlatformSettings");

            migrationBuilder.DropIndex(
                name: "IX_PlatformSettings_TenantId_Key",
                table: "PlatformSettings");

            migrationBuilder.DropIndex(
                name: "IX_ModuleLicenses_TenantId",
                table: "ModuleLicenses");

            migrationBuilder.DropIndex(
                name: "IX_ModuleLicenses_TenantId_ModuleKey",
                table: "ModuleLicenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ModuleLicenses");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_Key",
                table: "PlatformSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLicenses_ModuleKey",
                table: "ModuleLicenses",
                column: "ModuleKey",
                unique: true);
        }
    }
}
