using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Queries.ListProductCategories;

// Sin IRequirePermissions: lo consumen el alta/edición de producto y el filtro de reportes,
// no solo el ABM. Basta con estar autenticado.
public sealed record ListProductCategoriesQuery() : IRequest<Result<IReadOnlyList<ProductCategoryResponse>>>;

public sealed record ProductCategoryResponse(Guid Id, string Name);
