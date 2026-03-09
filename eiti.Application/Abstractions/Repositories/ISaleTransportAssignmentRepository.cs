using eiti.Domain.Companies;
using eiti.Domain.Sales;
using eiti.Domain.Transport;

namespace eiti.Application.Abstractions.Repositories;

public interface ISaleTransportAssignmentRepository
{
    Task AddAsync(SaleTransportAssignment assignment, CancellationToken cancellationToken = default);
    Task<SaleTransportAssignment?> GetByIdAsync(SaleTransportAssignmentId id, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<SaleTransportAssignment?> GetBySaleIdAsync(SaleId saleId, CompanyId companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleTransportAssignment>> ListBySaleIdsAsync(IReadOnlyList<SaleId> saleIds, CompanyId companyId, CancellationToken cancellationToken = default);
}
