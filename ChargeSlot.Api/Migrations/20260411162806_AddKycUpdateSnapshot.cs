using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKycUpdateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrevAddress",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevBackIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevBusinessLicenseNumber",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevBusinessLicenseUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevBusinessName",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevFrontIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevIdCardDate",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevIdCardNumber",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevTaxCode",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrevAddress",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevBackIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevBusinessLicenseNumber",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevBusinessLicenseUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevBusinessName",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevFrontIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevIdCardDate",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevIdCardNumber",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevTaxCode",
                table: "Owner");
        }
    }
}
