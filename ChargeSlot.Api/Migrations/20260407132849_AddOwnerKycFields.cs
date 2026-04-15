using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerKycFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessLicenseNumber",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessLicenseUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrontIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardDate",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardNumber",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KycRejectReason",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycReviewedAt",
                table: "Owner",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycReviewedByUserId",
                table: "Owner",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KycStatus",
                table: "Owner",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "KycSubmittedAt",
                table: "Owner",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Owner_KycReviewedByUserId",
                table: "Owner",
                column: "KycReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Owner_AspNetUsers_KycReviewedByUserId",
                table: "Owner",
                column: "KycReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Owner_AspNetUsers_KycReviewedByUserId",
                table: "Owner");

            migrationBuilder.DropIndex(
                name: "IX_Owner_KycReviewedByUserId",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "BackIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "BusinessLicenseNumber",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "BusinessLicenseUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "FrontIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "IdCardDate",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "IdCardNumber",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "KycRejectReason",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "KycReviewedAt",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "KycReviewedByUserId",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "KycSubmittedAt",
                table: "Owner");
        }
    }
}
