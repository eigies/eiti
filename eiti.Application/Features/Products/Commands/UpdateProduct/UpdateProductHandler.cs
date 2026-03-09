using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductHandler
    : IRequestHandler<UpdateProductCommand, Result<UpdateProductResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateProductResponse>> Handle(
        UpdateProductCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UpdateProductResponse>.Failure(
                Error.Unauthorized(
                    "Products.Update.Unauthorized",
                    "The current user is not authenticated."));
        }

        var product = await _productRepository.GetByIdAsync(
            new ProductId(request.Id),
            _currentUserService.CompanyId,
            cancellationToken);

        if (product is null)
        {
            return Result<UpdateProductResponse>.Failure(
                Error.NotFound(
                    "Products.Update.NotFound",
                    "The requested product was not found."));
        }

        var normalizedName = request.Name.Trim();
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        if (await _productRepository.NameExistsAsync(
                _currentUserService.CompanyId,
                normalizedName,
                product.Id,
                cancellationToken))
        {
            return Result<UpdateProductResponse>.Failure(
                Error.Conflict(
                    "Products.Update.NameAlreadyExists",
                    "Another product with the same name already exists."));
        }

        if (await _productRepository.CodeExistsAsync(
                _currentUserService.CompanyId,
                normalizedCode,
                product.Id,
                cancellationToken))
        {
            return Result<UpdateProductResponse>.Failure(
                Error.Conflict(
                    "Products.Update.CodeAlreadyExists",
                    "Another product with the same code already exists."));
        }

        if (await _productRepository.SkuExistsAsync(
                _currentUserService.CompanyId,
                normalizedSku,
                product.Id,
                cancellationToken))
        {
            return Result<UpdateProductResponse>.Failure(
                Error.Conflict(
                    "Products.Update.SkuAlreadyExists",
                    "Another product with the same SKU already exists."));
        }

        try
        {
            product.Update(
                request.Code,
                request.Sku,
                request.Brand,
                request.Name,
                request.Description,
                request.Price);
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateProductResponse>.Failure(
                Error.Validation("Products.Update.InvalidInput", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UpdateProductResponse>.Success(
            new UpdateProductResponse(
                product.Id.Value,
                product.Code,
                product.Sku,
                product.Brand,
                product.Name,
                product.Description,
                product.Price,
                0,
                0,
                0,
                product.CreatedAt,
                product.UpdatedAt));
    }
}
