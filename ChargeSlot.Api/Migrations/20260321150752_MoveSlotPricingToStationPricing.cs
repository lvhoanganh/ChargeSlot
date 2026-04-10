using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class MoveSlotPricingToStationPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlotPricing");

            migrationBuilder.CreateTable(
                name: "StationPricing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationPricing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationPricing_ChargingStation_StationId",
                        column: x => x.StationId,
                        principalTable: "ChargingStation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StationPricing_StationId_IsActive",
                table: "StationPricing",
                columns: new[] { "StationId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationPricing");

            migrationBuilder.CreateTable(
                name: "SlotPricing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DayOfWeek = table.Column<byte>(type: "tinyint", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PricePerHour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotPricing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlotPricing_ChargingSlot_SlotId",
                        column: x => x.SlotId,
                        principalTable: "ChargingSlot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlotPricing_SlotId_IsActive",
                table: "SlotPricing",
                columns: new[] { "SlotId", "IsActive" });
        }
    }
}
