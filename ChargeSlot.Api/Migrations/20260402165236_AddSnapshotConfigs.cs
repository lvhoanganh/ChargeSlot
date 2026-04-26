using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminReviewDeadlineAt",
                table: "Dispute",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerEvidenceDeadlineAt",
                table: "Dispute",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeRateSnapshot",
                table: "Booking",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "Refund100DeadlineAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Refund50DeadlineAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRateSnapshot",
                table: "Booking",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminReviewDeadlineAt",
                table: "Dispute");

            migrationBuilder.DropColumn(
                name: "OwnerEvidenceDeadlineAt",
                table: "Dispute");

            migrationBuilder.DropColumn(
                name: "PlatformFeeRateSnapshot",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "Refund100DeadlineAt",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "Refund50DeadlineAt",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "VatRateSnapshot",
                table: "Booking");
        }
    }
}
