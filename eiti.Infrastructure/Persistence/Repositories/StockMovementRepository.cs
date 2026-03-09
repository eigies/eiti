using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly ApplicationDbContext _context;

    public StockMovementRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovement>> ListAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.BranchId == branchId &&
                movement.ProductId == productId)
            .OrderByDescending(movement => movement.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
