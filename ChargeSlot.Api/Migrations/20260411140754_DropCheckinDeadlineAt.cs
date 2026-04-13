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
            migrationBuilder.DropColumn(
                name: "CheckinDeadlineAt",
                table: "Booking");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckinDeadlineAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);
        }
    }
}
