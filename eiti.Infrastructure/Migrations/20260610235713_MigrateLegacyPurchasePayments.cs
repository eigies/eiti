using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eiti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyPurchasePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migra los pagos por-compra (modelo viejo) al modelo de cuenta de proveedor.
            // Cada PurchasePayment real (no imputación de saldo a favor) se convierte en un SupplierPayment
            // reusando su mismo Id, y la fila PurchasePayment pasa a ser su imputación (apunta a sí misma).
            // Método 5 = SupplierCredit (imputación interna), se excluye. Estado 1 = Active.
            migrationBuilder.Sql(@"
                INSERT INTO SupplierPayments
                    (Id, CompanyId, SupplierId, BranchId, Method, Amount, Status, ChequeId, Reference, Notes, Date, CreatedAt, CreatedByUserId)
                SELECT
                    pp.Id, pu.CompanyId, pu.SupplierId, pu.BranchId, pp.Method, pp.Amount, pp.Status,
                    pp.ChequeId, pp.Reference, pp.Notes, pp.Date, pp.CreatedAt, pu.CreatedByUserId
                FROM PurchasePayments pp
                INNER JOIN Purchases pu ON pu.Id = pp.PurchaseId
                WHERE pp.SupplierPaymentId IS NULL
                  AND pp.Method <> 5
                  AND pp.Status = 1;");

            migrationBuilder.Sql(@"
                UPDATE pp
                SET pp.SupplierPaymentId = pp.Id
                FROM PurchasePayments pp
                WHERE pp.SupplierPaymentId IS NULL
                  AND pp.Method <> 5
                  AND pp.Status = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los SupplierPayments migrados comparten Id con su PurchasePayment de origen (autoreferencia).
            migrationBuilder.Sql(@"
                DELETE FROM SupplierPayments
                WHERE Id IN (SELECT Id FROM PurchasePayments WHERE SupplierPaymentId = Id);");

            migrationBuilder.Sql(@"
                UPDATE PurchasePayments SET SupplierPaymentId = NULL WHERE SupplierPaymentId = Id;");
        }
    }
}
