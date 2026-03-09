using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;

namespace eiti.Application.Abstractions.Repositories;

public interface IStockMovementRepository
{
    Task AddAsync(
        StockMovement movement,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);
}
