using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashMovementPaymentReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "CashMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SaleCcPaymentId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierPaymentId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CcPaymentGroupId",
                table: "CashMovements",
                column: "CcPaymentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SaleCcPaymentId",
                table: "CashMovements",
                column: "SaleCcPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_SupplierPaymentId",
                table: "CashMovements",
                column: "SupplierPaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CcPaymentGroupId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SaleCcPaymentId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_SupplierPaymentId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SaleCcPaymentId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "SupplierPaymentId",
                table: "CashMovements");
        }
    }
}
