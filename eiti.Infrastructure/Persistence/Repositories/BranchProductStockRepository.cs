using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class BranchProductStockRepository : IBranchProductStockRepository
{
    private readonly ApplicationDbContext _context;

    public BranchProductStockRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BranchProductStock?> GetByBranchAndProductAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BranchProductStocks
            .FirstOrDefaultAsync(
                stock => stock.BranchId == branchId
                    && stock.ProductId == productId
                    && stock.CompanyId == companyId,
                cancellationToken);
    }

    public async Task<BranchProductStock> GetOrCreateAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        var stock = await GetByBranchAndProductAsync(branchId, productId, companyId, cancellationToken);
        if (stock is not null)
        {
            return stock;
        }

        stock = BranchProductStock.Create(companyId, branchId, productId);
        await _context.BranchProductStocks.AddAsync(stock, cancellationToken);
        return stock;
    }

    public async Task<IReadOnlyList<BranchProductStock>> ListByBranchAsync(
        BranchId branchId,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BranchProductStocks
            .Where(stock => stock.BranchId == branchId && stock.CompanyId == companyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchProductStock>> ListByCompanyAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.BranchProductStocks
            .Where(stock => stock.CompanyId == companyId)
            .ToListAsync(cancellationToken);
    }
}
