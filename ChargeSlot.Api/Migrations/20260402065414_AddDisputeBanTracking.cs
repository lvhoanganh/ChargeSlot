using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeBanTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BanCount",
                table: "ChargingStation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedUntil",
                table: "ChargingStation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BanCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedUntil",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BanCount",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "BannedUntil",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "BanCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BannedUntil",
                table: "AspNetUsers");
        }
    }
}
