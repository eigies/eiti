using eiti.Domain.Branches;
using eiti.Domain.Companies;

namespace eiti.Application.Abstractions.Repositories;

public interface IFiscalPointOfSaleRepository
{
    /// <summary>El número ya lo usa otra sucursal de la empresa (sin contar <paramref name="excluding"/>).</summary>
    Task<bool> NumberInUseAsync(CompanyId companyId, int number, FiscalPointOfSaleId? excluding, CancellationToken cancellationToken = default);

    Task<FiscalPointOfSale?> GetByIdAsync(FiscalPointOfSaleId id, CompanyId companyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalPointOfSale>> ListByCompanyAsync(CompanyId companyId, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalPointOfSale pointOfSale, CancellationToken cancellationToken = default);

    void Remove(FiscalPointOfSale pointOfSale);
}
