using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCashDrawerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CashDrawerId",
                table: "Sales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CashDrawerId",
                table: "Sales",
                column: "CashDrawerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_CashDrawers_CashDrawerId",
                table: "Sales",
                column: "CashDrawerId",
                principalTable: "CashDrawers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_CashDrawers_CashDrawerId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CashDrawerId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CashDrawerId",
                table: "Sales");
        }
    }
}
