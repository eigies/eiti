using eiti.Domain.Companies;
using eiti.Domain.Products;

namespace eiti.Application.Abstractions.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        ProductId id,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetByCompanyIdAsync(
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        CompanyId companyId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(
        CompanyId companyId,
        string sku,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default);

    void Remove(Product product);

    Task<bool> NameExistsAsync(
        CompanyId companyId,
        string name,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(
        CompanyId companyId,
        string code,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default);

    Task<bool> SkuExistsAsync(
        CompanyId companyId,
        string sku,
        ProductId excludedProductId,
        CancellationToken cancellationToken = default);

    Task<bool> IsReferencedAsync(
        ProductId id,
        CancellationToken cancellationToken = default);
}
