using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdCardImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "FrontIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevBackIdCardUrl",
                table: "Owner");

            migrationBuilder.DropColumn(
                name: "PrevFrontIdCardUrl",
                table: "Owner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrontIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevBackIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevFrontIdCardUrl",
                table: "Owner",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
