using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServisTakip.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Products",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Products");
        }
    }
}
