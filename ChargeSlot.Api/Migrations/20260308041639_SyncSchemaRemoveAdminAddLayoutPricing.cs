using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncSchemaRemoveAdminAddLayoutPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingStation_AspNetUsers_ReviewedByUserId",
                table: "ChargingStation");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispute_AspNetUsers_ResolvedByUserId",
                table: "Dispute");

            migrationBuilder.DropForeignKey(
                name: "FK_PayoutRequest_AspNetUsers_ProcessedByUserId",
                table: "PayoutRequest");

            migrationBuilder.DropTable(
                name: "RolePermission");

            migrationBuilder.DropTable(
                name: "Permission");

            migrationBuilder.DropIndex(
                name: "IX_PayoutRequest_ProcessedByUserId",
                table: "PayoutRequest");

            migrationBuilder.DropIndex(
                name: "IX_Dispute_ResolvedByUserId",
                table: "Dispute");

            migrationBuilder.DropIndex(
                name: "IX_ChargingStation_ReviewedByUserId",
                table: "ChargingStation");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                table: "SlotPricing",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                table: "SlotPricing",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "SlotPricing",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutHeight",
                table: "ChargingStation",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayoutImageUrl",
                table: "ChargingStation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutWidth",
                table: "ChargingStation",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionX",
                table: "ChargingSlot",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionY",
                table: "ChargingSlot",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                table: "SlotPricing");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                table: "SlotPricing");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "SlotPricing");

            migrationBuilder.DropColumn(
                name: "LayoutHeight",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "LayoutImageUrl",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "LayoutWidth",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "ChargingSlot");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "ChargingSlot");

            migrationBuilder.CreateTable(
                name: "Permission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermission",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermission_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermission_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { 1, "a1", "Admin", "ADMIN" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutRequest_ProcessedByUserId",
                table: "PayoutRequest",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispute_ResolvedByUserId",
                table: "Dispute",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingStation_ReviewedByUserId",
                table: "ChargingStation",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Code",
                table: "Permission",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermission_PermissionId",
                table: "RolePermission",
                column: "PermissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingStation_AspNetUsers_ReviewedByUserId",
                table: "ChargingStation",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dispute_AspNetUsers_ResolvedByUserId",
                table: "Dispute",
                column: "ResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayoutRequest_AspNetUsers_ProcessedByUserId",
                table: "PayoutRequest",
                column: "ProcessedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
