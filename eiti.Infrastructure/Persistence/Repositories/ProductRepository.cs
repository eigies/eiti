using eiti.Application.Abstractions.Repositories;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace eiti.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(
        ProductId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(
                product => product.Id == id && product.CompanyId == companyId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IEnumerable<ProductId> ids,
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _context.Products
            .Where(product => product.CompanyId == companyId && idList.Contains(product.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByCompanyIdAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(product => product.CompanyId == companyId)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> SearchByCompanyAsync(
        CompanyId companyId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var words = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => word.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (words.Count == 0)
            return await GetByCompanyIdAsync(companyId, cancellationToken);

        var products = _context.Products.Where(product => product.CompanyId == companyId);

        foreach (var word in words)
        {
            var pattern = $"%{EscapeLike(word)}%";
            products = products.Where(product =>
                EF.Functions.ILike(EF.Functions.Unaccent(product.Name), EF.Functions.Unaccent(pattern), "\\")
                || EF.Functions.ILike(EF.Functions.Unaccent(product.Code), EF.Functions.Unaccent(pattern), "\\")
                || EF.Functions.ILike(EF.Functions.Unaccent(product.Sku), EF.Functions.Unaccent(pattern), "\\")
                || EF.Functions.ILike(EF.Functions.Unaccent(product.Brand), EF.Functions.Unaccent(pattern), "\\"));
        }

        var exact = query.Trim().ToLowerInvariant();

        return await products
            .OrderByDescending(product => product.Code.ToLower() == exact || product.Sku.ToLower() == exact)
            .ThenBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId && product.Name == name,
                cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        CompanyId companyId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId && product.Code == code,
                cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(
        CompanyId companyId,
        string sku,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId && product.Sku == sku,
                cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public void Remove(Product product)
    {
        _context.Products.Remove(product);
    }

    public async Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId
                    && product.Name == name
                    && product.Id != excludedProductId,
                cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(
        CompanyId companyId,
        string code,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId
                    && product.Code == code
                    && product.Id != excludedProductId,
                cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(
        CompanyId companyId,
        string sku,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(
                product => product.CompanyId == companyId
                    && product.Sku == sku
                    && product.Id != excludedProductId,
                cancellationToken);
    }

    public async Task<bool> IsReferencedAsync(
        ProductId id,
        CancellationToken cancellationToken = default)
    {
        return await _context.SaleDetails
            .AnyAsync(detail => detail.ProductId == id, cancellationToken);
    }
}
