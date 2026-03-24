using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChargingStationId",
                table: "Rating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerRepliedAt",
                table: "Rating",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReply",
                table: "Rating",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DisputeEvidence",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "ChargingStation",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalReviews",
                table: "ChargingStation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Rating_ChargingStationId",
                table: "Rating",
                column: "ChargingStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rating_ChargingStation_ChargingStationId",
                table: "Rating",
                column: "ChargingStationId",
                principalTable: "ChargingStation",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rating_ChargingStation_ChargingStationId",
                table: "Rating");

            migrationBuilder.DropIndex(
                name: "IX_Rating_ChargingStationId",
                table: "Rating");

            migrationBuilder.DropColumn(
                name: "ChargingStationId",
                table: "Rating");

            migrationBuilder.DropColumn(
                name: "OwnerRepliedAt",
                table: "Rating");

            migrationBuilder.DropColumn(
                name: "OwnerReply",
                table: "Rating");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "ChargingStation");

            migrationBuilder.DropColumn(
                name: "TotalReviews",
                table: "ChargingStation");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DisputeEvidence",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
