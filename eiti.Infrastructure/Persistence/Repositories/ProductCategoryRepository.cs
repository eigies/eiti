using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class ProductCategoryRepository : IProductCategoryRepository
{
    private readonly ApplicationDbContext _context;

    public ProductCategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductCategory?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductCategories
            .FirstOrDefaultAsync(category => category.Id == id && category.CompanyId == companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductCategory>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.ProductCategories
            .Where(category => category.CompanyId == companyId)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(Guid companyId, string name, Guid? excludedId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return await _context.ProductCategories.AnyAsync(
            category => category.CompanyId == companyId
                && category.Name.ToLower() == normalized
                && (excludedId == null || category.Id != excludedId),
            cancellationToken);
    }

    public async Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        await _context.ProductCategories.AddAsync(category, cancellationToken);
    }

    public void Update(ProductCategory category)
    {
        _context.ProductCategories.Update(category);
    }

    public void Delete(ProductCategory category)
    {
        _context.ProductCategories.Remove(category);
    }
}
