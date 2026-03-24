using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChargeSlot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceUploader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UploadedByUserId",
                table: "DisputeEvidence",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_UploadedByUserId",
                table: "DisputeEvidence",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeEvidence_AspNetUsers_UploadedByUserId",
                table: "DisputeEvidence",
                column: "UploadedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisputeEvidence_AspNetUsers_UploadedByUserId",
                table: "DisputeEvidence");

            migrationBuilder.DropIndex(
                name: "IX_DisputeEvidence_UploadedByUserId",
                table: "DisputeEvidence");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "DisputeEvidence");
        }
    }
}
