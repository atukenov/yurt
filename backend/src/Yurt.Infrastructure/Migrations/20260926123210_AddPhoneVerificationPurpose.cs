using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneVerificationPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_MobileNumber",
                table: "PhoneVerifications");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerUserId",
                table: "PhoneVerifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "PhoneVerifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_MobileNumber_Purpose",
                table: "PhoneVerifications",
                columns: new[] { "MobileNumber", "Purpose" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoneVerifications_MobileNumber_Purpose",
                table: "PhoneVerifications");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "PhoneVerifications");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "PhoneVerifications");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_MobileNumber",
                table: "PhoneVerifications",
                column: "MobileNumber",
                unique: true);
        }
    }
}
