using eiti.Domain.Branches;
using eiti.Domain.Cash;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;

namespace eiti.Application.Abstractions.Repositories;

public interface ISaleRepository
{
    Task AddAsync(
        Sale sale,
        CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdAsync(
        SaleId id,
        CancellationToken cancellationToken = default);

    void Remove(Sale sale);

    Task<IReadOnlyList<Sale>> ListByCompanyAsync(
        CompanyId companyId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int? idSaleStatus,
        bool includeCuentaCorriente = false,
        CancellationToken cancellationToken = default);

    // Reporte de ventas: empuja a SQL los filtros baratos (sucursal, cliente, excluye canceladas,
    // sucursales permitidas) en el rango de fechas. AsNoTracking + solo Include(Details). El resto de
    // los filtros (canal, entrega, tipo, instalador, vehículo, categoría) se resuelven en el handler.
    Task<IReadOnlyList<Sale>> ListForSalesReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        Guid? customerId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    // Cantidad de ventas canceladas del período. El dashboard la necesita para el pulso del
    // día, y ListForSalesReportAsync las excluye por diseño. Un COUNT evita traerlas.
    Task<int> CountCancelledAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    // Detalle liviano del dashboard. Incluye canceladas y no carga colecciones relacionadas.
    Task<IReadOnlyList<Sale>> ListForDashboardAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    // Reporte de canjes: mismos filtros que el reporte de ventas pero con Include(TradeIns) en vez de
    // Details, y solo ventas que efectivamente recibieron algo en canje. AsNoTracking.
    Task<IReadOnlyList<Sale>> ListForTradeInReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        Guid? customerId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    // Ventas no canceladas (con sus pagos) para el reporte de medios de pago. AsNoTracking + Include(Payments).
    Task<IReadOnlyList<Sale>> ListWithPaymentsForReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? branchId,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    Task<bool> HasOnHoldSalesByCashDrawerAsync(
        CompanyId companyId,
        CashDrawerId cashDrawerId,
        CancellationToken cancellationToken = default);

    Task<bool> HasInTransitSalesByCashDrawerAsync(
        CompanyId companyId,
        CashDrawerId cashDrawerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalePayment>> GetPaymentsBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalePayment>> GetPaymentsByCashSessionIdAsync(
        CashSessionId sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalePayment>> GetPaymentsByCashSessionIdsAsync(
        IEnumerable<CashSessionId> sessionIds,
        CancellationToken cancellationToken = default);

    Task<int> CountByBranchAsync(
        BranchId branchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> SearchDeliveryAddressesAsync(
        string query,
        CompanyId companyId,
        int limit = 8,
        CancellationToken cancellationToken = default);

    Task<Sale?> GetByIdWithCcPaymentsAsync(
        SaleId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> ListCcSalesByCompanyAsync(
        CompanyId companyId,
        CustomerId? customerId,
        CancellationToken cancellationToken = default);

    // Bolsa por cliente: ventas CC activas con saldo pendiente > 0, más vieja primero (fuente FIFO). Tracked.
    Task<IReadOnlyList<Sale>> ListPendingCcSalesByCustomerAsync(
        CompanyId companyId,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    // Todas las ventas CC del cliente (cualquier estado) para la bolsa/cuenta. Tracked.
    Task<IReadOnlyList<Sale>> ListCcSalesByCustomerAsync(
        CompanyId companyId,
        CustomerId customerId,
        CancellationToken cancellationToken = default);

    // Ventas con imputaciones internas (SaleCcPayment) generadas por un cobro a nivel cliente. Tracked, para revertir.
    Task<IReadOnlyList<Sale>> ListByCustomerPaymentIdAsync(
        CompanyId companyId,
        Guid customerPaymentId,
        CancellationToken cancellationToken = default);

    // Ventas con imputaciones activas de una nota de crédito, para poder deshacerlas al anularla. Tracked.
    Task<IReadOnlyList<Sale>> ListByCreditNoteIdAsync(
        CompanyId companyId,
        Guid creditNoteId,
        CancellationToken cancellationToken = default);

    // Saldo CC pendiente por cliente (ventas CC activas con pendiente > 0). Para la lista de cuentas.
    Task<Dictionary<Guid, decimal>> GetPendingCcTotalsByCustomerAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> ListReservingByProductAsync(
        CompanyId companyId,
        ProductId productId,
        BranchId? branchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, Guid>> GetSaleIdsByCcPaymentIdsAsync(
        IEnumerable<Guid> ccPaymentIds,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string?>> GetCodesByCustomerPaymentIdsAsync(
        IEnumerable<Guid> customerPaymentIds,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string?>> GetCodesBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleCcPayment>> GetCcPaymentsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken = default);
}
