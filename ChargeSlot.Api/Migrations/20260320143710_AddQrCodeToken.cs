using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQrCodeToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrCodeToken",
                table: "ChargingSlot",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargingSlot_QrCodeToken",
                table: "ChargingSlot",
                column: "QrCodeToken",
                unique: true,
                filter: "[QrCodeToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChargingSlot_QrCodeToken",
                table: "ChargingSlot");

            migrationBuilder.DropColumn(
                name: "QrCodeToken",
                table: "ChargingSlot");
        }
    }
}
