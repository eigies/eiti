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

    public async Task<IReadOnlyList<StockMovement>> ListByReferenceAsync(
        Guid referenceId,
        string referenceType,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.ReferenceType == referenceType &&
                movement.ReferenceId == referenceId)
            .OrderBy(movement => movement.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovement>> ListForReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? productId,
        Guid? branchId,
        int? type,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.CompanyId == companyId &&
                movement.CreatedAt >= from &&
                movement.CreatedAt <= to);

        if (productId.HasValue)
        {
            var pId = new ProductId(productId.Value);
            query = query.Where(movement => movement.ProductId == pId);
        }

        if (branchId.HasValue)
        {
            var bId = new BranchId(branchId.Value);
            query = query.Where(movement => movement.BranchId == bId);
        }

        if (type.HasValue)
        {
            var movementType = (StockMovementType)type.Value;
            query = query.Where(movement => movement.Type == movementType);
        }

        if (allowedBranchIds is not null && allowedBranchIds.Count > 0)
        {
            var allowed = allowedBranchIds.Select(id => new BranchId(id)).ToList();
            query = query.Where(movement => allowed.Contains(movement.BranchId));
        }

        return await query
            .OrderByDescending(movement => movement.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
