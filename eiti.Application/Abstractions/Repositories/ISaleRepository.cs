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

    Task<bool> HasOnHoldSalesByCashDrawerAsync(
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

    Task<Dictionary<Guid, string?>> GetCodesBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleCcPayment>> GetCcPaymentsByGroupIdsAsync(
        IEnumerable<Guid> groupIds,
        CancellationToken cancellationToken = default);
}
