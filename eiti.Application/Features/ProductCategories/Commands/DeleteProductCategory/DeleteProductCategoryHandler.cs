using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using MediatR;

namespace eiti.Application.Features.ProductCategories.Commands.DeleteProductCategory;

public sealed class DeleteProductCategoryHandler : IRequestHandler<DeleteProductCategoryCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductCategoryHandler(
        ICurrentUserService currentUserService,
        IProductCategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteProductCategoryCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var category = await _categoryRepository.GetByIdAsync(command.Id, companyId.Value, cancellationToken);
        if (category is null)
            return Result.Failure(DeleteProductCategoryErrors.NotFound);

        // Borrado físico: la FK con OnDelete(SetNull) des-etiqueta los productos que la usaban.
        _categoryRepository.Delete(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
