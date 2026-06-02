using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServisTakip.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianNoteToOfferLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TechnicianNote",
                table: "OfferLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TechnicianNote",
                table: "OfferLines");
        }
    }
}
