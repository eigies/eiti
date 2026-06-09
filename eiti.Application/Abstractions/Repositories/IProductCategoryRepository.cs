using eiti.Domain.Products;

namespace eiti.Application.Abstractions.Repositories;

public interface IProductCategoryRepository
{
    Task<ProductCategory?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductCategory>> ListByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid companyId, string name, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task AddAsync(ProductCategory category, CancellationToken cancellationToken = default);

    void Update(ProductCategory category);

    void Delete(ProductCategory category);
}
