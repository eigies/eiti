using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchFiscalPointOfSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FiscalPointOfSaleId",
                table: "Branches",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FiscalPointsOfSale",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPointsOfSale", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_FiscalPointOfSaleId",
                table: "Branches",
                column: "FiscalPointOfSaleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPointsOfSale_CompanyId_Number",
                table: "FiscalPointsOfSale",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_FiscalPointsOfSale_FiscalPointOfSaleId",
                table: "Branches",
                column: "FiscalPointOfSaleId",
                principalTable: "FiscalPointsOfSale",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Branches_FiscalPointsOfSale_FiscalPointOfSaleId",
                table: "Branches");

            migrationBuilder.DropTable(
                name: "FiscalPointsOfSale");

            migrationBuilder.DropIndex(
                name: "IX_Branches_FiscalPointOfSaleId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "FiscalPointOfSaleId",
                table: "Branches");
        }
    }
}
