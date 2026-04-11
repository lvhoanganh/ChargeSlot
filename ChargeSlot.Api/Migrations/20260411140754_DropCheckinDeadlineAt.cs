using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropCheckinDeadlineAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Wallet",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DropColumn(
                name: "CheckinDeadlineAt",
                table: "Booking");

            migrationBuilder.InsertData(
                table: "Wallet",
                columns: new[] { "Id", "AvailableBalance", "CreatedAt", "FrozenBalance", "SystemCode", "UserId", "WalletType" },
                values: new object[] { 99, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "TAX_HOLD", null, "System" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Wallet",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckinDeadlineAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Wallet",
                columns: new[] { "Id", "AvailableBalance", "CreatedAt", "FrozenBalance", "SystemCode", "UserId", "WalletType" },
                values: new object[] { 4, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, "TAX_HOLD", null, "System" });
        }
    }
}
