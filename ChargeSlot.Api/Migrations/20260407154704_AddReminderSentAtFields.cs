using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSentAtFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "WithdrawRequest",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Invoice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminReminderSentAt",
                table: "Dispute",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerReminderSentAt",
                table: "Dispute",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "WithdrawRequest");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Invoice");

            migrationBuilder.DropColumn(
                name: "AdminReminderSentAt",
                table: "Dispute");

            migrationBuilder.DropColumn(
                name: "OwnerReminderSentAt",
                table: "Dispute");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Booking");
        }
    }
}
