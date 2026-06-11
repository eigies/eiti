using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupplierAccountBag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migración de datos: el proveedor pasa a ser obligatorio. Las compras huérfanas
            // (SupplierId NULL) se asignan a un proveedor "Sin proveedor" creado por compañía.
            migrationBuilder.Sql(@"
                INSERT INTO Suppliers (Id, CompanyId, Name, IsActive, CreatedAt, CreditBalance)
                SELECT NEWID(), c.CompanyId, 'Sin proveedor', 1, SYSUTCDATETIME(), 0
                FROM (SELECT DISTINCT CompanyId FROM Purchases WHERE SupplierId IS NULL) c
                WHERE NOT EXISTS (
                    SELECT 1 FROM Suppliers s
                    WHERE s.CompanyId = c.CompanyId AND s.Name = 'Sin proveedor');");

            migrationBuilder.Sql(@"
                UPDATE pu
                SET pu.SupplierId = s.Id
                FROM Purchases pu
                INNER JOIN Suppliers s
                    ON s.CompanyId = pu.CompanyId AND s.Name = 'Sin proveedor'
                WHERE pu.SupplierId IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "Purchases",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierPaymentId",
                table: "PurchasePayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChequeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPayments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_SupplierPaymentId",
                table: "PurchasePayments",
                column: "SupplierPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_CompanyId_SupplierId",
                table: "SupplierPayments",
                columns: new[] { "CompanyId", "SupplierId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_Status",
                table: "SupplierPayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPayments_SupplierId",
                table: "SupplierPayments",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierPayments");

            migrationBuilder.DropIndex(
                name: "IX_PurchasePayments_SupplierPaymentId",
                table: "PurchasePayments");

            migrationBuilder.DropColumn(
                name: "SupplierPaymentId",
                table: "PurchasePayments");

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "Purchases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
