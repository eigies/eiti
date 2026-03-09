using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;

namespace eiti.Application.Abstractions.Repositories;

public interface IBranchProductStockRepository
{
    Task<BranchProductStock?> GetByBranchAndProductAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<BranchProductStock> GetOrCreateAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchProductStock>> ListByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchProductStock>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);
}
