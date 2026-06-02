using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServisTakip.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToArchiveRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "ArchiveRepairs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "ArchiveRepairs");
        }
    }
}
