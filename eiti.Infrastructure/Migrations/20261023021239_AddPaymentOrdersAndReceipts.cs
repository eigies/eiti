using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrdersAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentOrderId",
                table: "SupplierPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiptId",
                table: "CustomerPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPaymentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_PaymentOrderId",
                table: "SupplierPayments",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_ReceiptId",
                table: "CustomerPayments",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_CompanyId_Code",
                table: "CustomerReceipts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_CompanyId_CustomerId",
                table: "CustomerReceipts",
                columns: new[] { "CompanyId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_Status",
                table: "CustomerReceipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentOrders_CompanyId_Code",
                table: "SupplierPaymentOrders",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentOrders_CompanyId_SupplierId",
                table: "SupplierPaymentOrders",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentOrders_Status",
                table: "SupplierPaymentOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentOrders_SupplierId",
                table: "SupplierPaymentOrders",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_CustomerReceipts_ReceiptId",
                table: "CustomerPayments",
                column: "ReceiptId",
                principalTable: "CustomerReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPayments_SupplierPaymentOrders_PaymentOrderId",
                table: "SupplierPayments",
                column: "PaymentOrderId",
                principalTable: "SupplierPaymentOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_CustomerReceipts_ReceiptId",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPayments_SupplierPaymentOrders_PaymentOrderId",
                table: "SupplierPayments");

            migrationBuilder.DropTable(
                name: "CustomerReceipts");

            migrationBuilder.DropTable(
                name: "SupplierPaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_SupplierPayments_PaymentOrderId",
                table: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_ReceiptId",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "PaymentOrderId",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "ReceiptId",
                table: "CustomerPayments");
        }
    }
}
