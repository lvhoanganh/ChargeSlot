using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddManualCheckinRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ManualCheckinRequestedAt",
                table: "Booking",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualCheckinRequestedAt",
                table: "Booking");
        }
    }
}
