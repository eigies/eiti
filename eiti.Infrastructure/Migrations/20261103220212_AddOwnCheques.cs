using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnCheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Origen",
                table: "Cheques",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "Cheques",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_CompanyId_BankId_Numero",
                table: "Cheques",
                columns: new[] { "CompanyId", "BankId", "Numero" },
                unique: true,
                filter: "\"Origen\" = 2 AND \"Estado\" <> 5");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SupplierId",
                table: "Cheques",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cheques_CompanyId_BankId_Numero",
                table: "Cheques");

            migrationBuilder.DropIndex(
                name: "IX_Cheques_SupplierId",
                table: "Cheques");

            migrationBuilder.DropColumn(
                name: "Origen",
                table: "Cheques");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "Cheques");
        }
    }
}
