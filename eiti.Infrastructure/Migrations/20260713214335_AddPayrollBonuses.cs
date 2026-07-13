using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollBonusConcepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBonusConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBonuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayrollLiquidationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBonuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLiquidationBonusLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollLiquidationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollBonusId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AmountType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLiquidationBonusLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLiquidationBonusLines_PayrollLiquidations_PayrollLiq~",
                        column: x => x.PayrollLiquidationId,
                        principalTable: "PayrollLiquidations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBonusConcepts_CompanyId_IsActive",
                table: "PayrollBonusConcepts",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBonuses_CompanyId_EmployeeId_Status",
                table: "PayrollBonuses",
                columns: new[] { "CompanyId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLiquidationBonusLines_PayrollLiquidationId",
                table: "PayrollLiquidationBonusLines",
                column: "PayrollLiquidationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBonusConcepts");

            migrationBuilder.DropTable(
                name: "PayrollBonuses");

            migrationBuilder.DropTable(
                name: "PayrollLiquidationBonusLines");
        }
    }
}
