using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingFlowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Booking",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "Booking",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "Booking");
        }
    }
}
