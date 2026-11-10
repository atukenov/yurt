using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockPack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "StockChanges",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OtherLocationId",
                table: "StockChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherLocationName",
                table: "StockChanges",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "StockChanges",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "StockChanges",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "set");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "StockChanges",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerUnit",
                table: "Ingredients",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Note",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "OtherLocationId",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "OtherLocationName",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "StockChanges");

            migrationBuilder.DropColumn(
                name: "CostPerUnit",
                table: "Ingredients");
        }
    }
}
