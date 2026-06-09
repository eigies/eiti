using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Commands.CreateProductCategory;

public sealed class CreateProductCategoryHandler
    : IRequestHandler<CreateProductCategoryCommand, Result<CreateProductCategoryResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCategoryHandler(
        ICurrentUserService currentUserService,
        IProductCategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateProductCategoryResponse>> Handle(CreateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateProductCategoryResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        if (await _categoryRepository.NameExistsAsync(companyId.Value, command.Name, null, cancellationToken))
            return Result<CreateProductCategoryResponse>.Failure(CreateProductCategoryErrors.NameAlreadyExists);

        ProductCategory category;
        try
        {
            category = ProductCategory.Create(companyId.Value, command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateProductCategoryResponse>.Failure(
                Error.Validation("ProductCategories.Create.InvalidInput", ex.Message));
        }

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateProductCategoryResponse>.Success(new CreateProductCategoryResponse(category.Id, category.Name));
    }
}
