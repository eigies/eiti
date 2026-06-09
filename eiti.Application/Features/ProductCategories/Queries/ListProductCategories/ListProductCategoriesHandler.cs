using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Queries.ListProductCategories;

public sealed class ListProductCategoriesHandler
    : IRequestHandler<ListProductCategoriesQuery, Result<IReadOnlyList<ProductCategoryResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductCategoryRepository _categoryRepository;

    public ListProductCategoriesHandler(
        ICurrentUserService currentUserService,
        IProductCategoryRepository categoryRepository)
    {
        _currentUserService = currentUserService;
        _categoryRepository = categoryRepository;
    }

    public async Task<Result<IReadOnlyList<ProductCategoryResponse>>> Handle(ListProductCategoriesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ProductCategoryResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var categories = await _categoryRepository.ListByCompanyAsync(companyId.Value, cancellationToken);

        return Result<IReadOnlyList<ProductCategoryResponse>>.Success(
            categories.Select(c => new ProductCategoryResponse(c.Id, c.Name)).ToList());
    }
}
