using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovementSaleIncomeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReferenceId_Type",
                table: "CashMovements",
                columns: new[] { "ReferenceId", "Type" },
                unique: true,
                filter: "[ReferenceType] = 'Sale' AND [Type] IN (2, 10, 11)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashMovements_ReferenceId_Type",
                table: "CashMovements");
        }
    }
}
