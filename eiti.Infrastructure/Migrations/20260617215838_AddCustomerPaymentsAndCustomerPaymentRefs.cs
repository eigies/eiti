using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPaymentsAndCustomerPaymentRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentId",
                table: "SaleCcPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPaymentId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChequeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardBankId = table.Column<int>(type: "integer", nullable: true),
                    CardCuotas = table.Column<int>(type: "integer", nullable: true),
                    CardSurchargePct = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CardSurchargeAmt = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalCobrado = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleCcPayments_CustomerPaymentId",
                table: "SaleCcPayments",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CustomerPaymentId",
                table: "CashMovements",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CompanyId_CustomerId",
                table: "CustomerPayments",
                columns: new[] { "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_Status",
                table: "CustomerPayments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_SaleCcPayments_CustomerPaymentId",
                table: "SaleCcPayments");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_CustomerPaymentId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentId",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentId",
                table: "CashMovements");
        }
    }
}
