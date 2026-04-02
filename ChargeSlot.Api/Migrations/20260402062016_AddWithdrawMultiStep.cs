using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawMultiStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssueNote",
                table: "WithdrawRequest",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueReportedAt",
                table: "WithdrawRequest",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferReceiptUrl",
                table: "WithdrawRequest",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TransferredAt",
                table: "WithdrawRequest",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UserConfirmedAt",
                table: "WithdrawRequest",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssueNote",
                table: "WithdrawRequest");

            migrationBuilder.DropColumn(
                name: "IssueReportedAt",
                table: "WithdrawRequest");

            migrationBuilder.DropColumn(
                name: "TransferReceiptUrl",
                table: "WithdrawRequest");

            migrationBuilder.DropColumn(
                name: "TransferredAt",
                table: "WithdrawRequest");

            migrationBuilder.DropColumn(
                name: "UserConfirmedAt",
                table: "WithdrawRequest");
        }
    }
}
