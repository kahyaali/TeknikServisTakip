using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServisTakip.Migrations
{
    /// <inheritdoc />
    public partial class AddCariNoAndCompanyNameToAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CariNo",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CariNo",
                table: "AspNetUsers",
                column: "CariNo",
                unique: true,
                filter: "[CariNo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CariNo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CariNo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "AspNetUsers");
        }
    }
}
