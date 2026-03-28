using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBanksChequesAndCardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CardBankId",
                table: "SalePayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardCuotas",
                table: "SalePayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardSurchargeAmt",
                table: "SalePayments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardSurchargePct",
                table: "SalePayments",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCobrado",
                table: "SalePayments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardBankId",
                table: "SaleCcPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CardCuotas",
                table: "SaleCcPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardSurchargeAmt",
                table: "SaleCcPayments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CardSurchargePct",
                table: "SaleCcPayments",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCobrado",
                table: "SaleCcPayments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cheques",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalePaymentSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SalePaymentMethod = table.Column<int>(type: "int", nullable: true),
                    SaleCcPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Titular = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CuitDni = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cheques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankInstallmentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    Cuotas = table.Column<int>(type: "int", nullable: false),
                    SurchargePct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankInstallmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankInstallmentPlans_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankInstallmentPlans_BankId_Cuotas",
                table: "BankInstallmentPlans",
                columns: new[] { "BankId", "Cuotas" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_BankId",
                table: "Cheques",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Estado",
                table: "Cheques",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_FechaVencimiento",
                table: "Cheques",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SaleCcPaymentId",
                table: "Cheques",
                column: "SaleCcPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_SalePaymentSaleId",
                table: "Cheques",
                column: "SalePaymentSaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankInstallmentPlans");

            migrationBuilder.DropTable(
                name: "Cheques");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropColumn(
                name: "CardBankId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "CardCuotas",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "CardSurchargeAmt",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "CardSurchargePct",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "TotalCobrado",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "CardBankId",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "CardCuotas",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "CardSurchargeAmt",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "CardSurchargePct",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "TotalCobrado",
                table: "SaleCcPayments");
        }
    }
}
