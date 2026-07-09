using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseSalary",
                table: "Employees",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollPeriodicity",
                table: "Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollAdvanceId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollLiquidationId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayrollAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedToLiquidationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollAdvances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollDeductionConcepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductionConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLiquidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodLabel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLiquidations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLiquidationAdvanceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollLiquidationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollAdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLiquidationAdvanceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLiquidationAdvanceLines_PayrollLiquidations_PayrollL~",
                        column: x => x.PayrollLiquidationId,
                        principalTable: "PayrollLiquidations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLiquidationDeductionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollLiquidationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLiquidationDeductionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLiquidationDeductionLines_PayrollLiquidations_Payrol~",
                        column: x => x.PayrollLiquidationId,
                        principalTable: "PayrollLiquidations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_PayrollAdvanceId",
                table: "CashMovements",
                column: "PayrollAdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_PayrollLiquidationId",
                table: "CashMovements",
                column: "PayrollLiquidationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdvances_CompanyId_EmployeeId_Status",
                table: "PayrollAdvances",
                columns: new[] { "CompanyId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionConcepts_CompanyId_IsActive",
                table: "PayrollDeductionConcepts",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLiquidationAdvanceLines_PayrollLiquidationId",
                table: "PayrollLiquidationAdvanceLines",
                column: "PayrollLiquidationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLiquidationDeductionLines_PayrollLiquidationId",
                table: "PayrollLiquidationDeductionLines",
                column: "PayrollLiquidationId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLiquidations_CompanyId_EmployeeId_PeriodLabel",
                table: "PayrollLiquidations",
                columns: new[] { "CompanyId", "EmployeeId", "PeriodLabel" },
                unique: true,
                filter: "\"Status\" <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollAdvances");

            migrationBuilder.DropTable(
                name: "PayrollDeductionConcepts");

            migrationBuilder.DropTable(
                name: "PayrollLiquidationAdvanceLines");

            migrationBuilder.DropTable(
                name: "PayrollLiquidationDeductionLines");

            migrationBuilder.DropTable(
                name: "PayrollLiquidations");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_PayrollAdvanceId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_PayrollLiquidationId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "BaseSalary",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PayrollPeriodicity",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PayrollAdvanceId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "PayrollLiquidationId",
                table: "CashMovements");
        }
    }
}
