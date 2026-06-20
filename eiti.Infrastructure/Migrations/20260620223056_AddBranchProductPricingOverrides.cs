using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchProductPricingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostOverride",
                table: "BranchProductStocks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePriceOverride",
                table: "BranchProductStocks",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostOverride",
                table: "BranchProductStocks");

            migrationBuilder.DropColumn(
                name: "SalePriceOverride",
                table: "BranchProductStocks");
        }
    }
}
