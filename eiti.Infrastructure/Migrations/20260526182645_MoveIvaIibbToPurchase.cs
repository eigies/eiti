using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveIvaIibbToPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IngresosBrutosPct",
                table: "PurchasePayments");

            migrationBuilder.DropColumn(
                name: "IvaPct",
                table: "PurchasePayments");

            migrationBuilder.AddColumn<decimal>(
                name: "IngresosBrutosPct",
                table: "Purchases",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaPct",
                table: "Purchases",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IngresosBrutosPct",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "IvaPct",
                table: "Purchases");

            migrationBuilder.AddColumn<decimal>(
                name: "IngresosBrutosPct",
                table: "PurchasePayments",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IvaPct",
                table: "PurchasePayments",
                type: "decimal(5,2)",
                nullable: true);
        }
    }
}
