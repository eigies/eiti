using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Commands.UpdateProductCategory;

public sealed class UpdateProductCategoryHandler : IRequestHandler<UpdateProductCategoryCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCategoryHandler(
        ICurrentUserService currentUserService,
        IProductCategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateProductCategoryCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var category = await _categoryRepository.GetByIdAsync(command.Id, companyId.Value, cancellationToken);
        if (category is null)
            return Result.Failure(UpdateProductCategoryErrors.NotFound);

        if (await _categoryRepository.NameExistsAsync(companyId.Value, command.Name, command.Id, cancellationToken))
            return Result.Failure(UpdateProductCategoryErrors.NameAlreadyExists);

        try
        {
            category.Rename(command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(Error.Validation("ProductCategories.Update.InvalidInput", ex.Message));
        }

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
