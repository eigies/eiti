using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditNoteId",
                table: "SaleCcPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditNoteId",
                table: "PurchasePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerCreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCreditNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCreditNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleCcPayments_CreditNoteId",
                table: "SaleCcPayments",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_CreditNoteId",
                table: "PurchasePayments",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditNotes_CompanyId_CustomerId",
                table: "CustomerCreditNotes",
                columns: new[] { "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCreditNotes_Status",
                table: "CustomerCreditNotes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_CompanyId_SupplierId",
                table: "SupplierCreditNotes",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCreditNotes_Status",
                table: "SupplierCreditNotes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCreditNotes");

            migrationBuilder.DropTable(
                name: "SupplierCreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_SaleCcPayments_CreditNoteId",
                table: "SaleCcPayments");

            migrationBuilder.DropIndex(
                name: "IX_PurchasePayments_CreditNoteId",
                table: "PurchasePayments");

            migrationBuilder.DropColumn(
                name: "CreditNoteId",
                table: "SaleCcPayments");

            migrationBuilder.DropColumn(
                name: "CreditNoteId",
                table: "PurchasePayments");
        }
    }
}
