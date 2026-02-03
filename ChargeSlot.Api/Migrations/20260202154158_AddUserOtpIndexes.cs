using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOtpIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "UserOtps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserOtps_PhoneNumber",
                table: "UserOtps",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_UserOtps_PhoneNumber_IsUsed_ExpiredAt",
                table: "UserOtps",
                columns: new[] { "PhoneNumber", "IsUsed", "ExpiredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserOtps_PhoneNumber",
                table: "UserOtps");

            migrationBuilder.DropIndex(
                name: "IX_UserOtps_PhoneNumber_IsUsed_ExpiredAt",
                table: "UserOtps");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "UserOtps",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
