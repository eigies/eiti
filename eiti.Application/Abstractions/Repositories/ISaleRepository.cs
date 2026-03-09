using eiti.Domain.Companies;
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
}
