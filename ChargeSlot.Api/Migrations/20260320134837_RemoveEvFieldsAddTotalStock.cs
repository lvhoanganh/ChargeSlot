using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEvFieldsAddTotalStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectorType",
                table: "ChargingSlot");

            migrationBuilder.DropColumn(
                name: "PowerKw",
                table: "ChargingSlot");

            migrationBuilder.DropColumn(
                name: "EnergyKwh",
                table: "ChargingSession");

            migrationBuilder.AddColumn<int>(
                name: "TotalStock",
                table: "ExtraService",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalStock",
                table: "ExtraService");

            migrationBuilder.AddColumn<string>(
                name: "ConnectorType",
                table: "ChargingSlot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PowerKw",
                table: "ChargingSlot",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyKwh",
                table: "ChargingSession",
                type: "decimal(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);
        }
    }
}
