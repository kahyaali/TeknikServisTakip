using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServisTakip.Migrations
{
    /// <inheritdoc />
    public partial class AddReviseArchiveTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviseArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfferId = table.Column<int>(type: "int", nullable: false),
                    OfferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RepairItemId = table.Column<int>(type: "int", nullable: false),
                    CustomerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovedOfferId = table.Column<int>(type: "int", nullable: false),
                    ApprovedOfferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApprovedVersion = table.Column<int>(type: "int", nullable: false),
                    TotalSnapshotData = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviseArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviseArchives_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviseArchives_RepairItems_RepairItemId",
                        column: x => x.RepairItemId,
                        principalTable: "RepairItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviseArchives_CustomerNumber",
                table: "ReviseArchives",
                column: "CustomerNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ReviseArchives_OfferId",
                table: "ReviseArchives",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviseArchives_OfferNumber",
                table: "ReviseArchives",
                column: "OfferNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ReviseArchives_RepairItemId",
                table: "ReviseArchives",
                column: "RepairItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviseArchives_RevokedAt",
                table: "ReviseArchives",
                column: "RevokedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviseArchives");
        }
    }
}
