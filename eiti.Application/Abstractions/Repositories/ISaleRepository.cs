using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
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
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalePayment>> GetPaymentsBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
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

    Task<IReadOnlyList<Sale>> GetByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, Guid>> GetSaleIdsByCcPaymentIdsAsync(
        IEnumerable<Guid> ccPaymentIds,
        CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, string?>> GetCodesBySaleIdsAsync(
        IEnumerable<Guid> saleIds,
        CancellationToken cancellationToken = default);
}
