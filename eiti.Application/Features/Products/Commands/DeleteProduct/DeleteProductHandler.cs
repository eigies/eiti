using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Products.Commands.DeleteProduct;

public sealed class DeleteProductHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteProductCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var product = await _productRepository.GetByIdAsync(
            new ProductId(request.Id),
            _currentUserService.CompanyId,
            cancellationToken);

        if (product is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Products.Delete.NotFound",
                    "The requested product was not found."));
        }

        if (await _productRepository.IsReferencedAsync(product.Id, cancellationToken))
        {
            return Result.Failure(
                Error.Conflict(
                    "Products.Delete.InUse",
                    "The product cannot be deleted because it is already used in a sale."));
        }

        _productRepository.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
