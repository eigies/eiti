using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductHandler
    : IRequestHandler<CreateProductCommand, Result<CreateProductResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateProductResponse>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<CreateProductResponse>.Failure(CreateProductErrors.Unauthorized);
        }

        var normalizedName = request.Name.Trim();
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        if (await _productRepository.NameExistsAsync(
                _currentUserService.CompanyId,
                normalizedName,
                cancellationToken))
        {
            return Result<CreateProductResponse>.Failure(
                CreateProductErrors.ProductNameAlreadyExists);
        }

        if (await _productRepository.CodeExistsAsync(
                _currentUserService.CompanyId,
                normalizedCode,
                cancellationToken))
        {
            return Result<CreateProductResponse>.Failure(
                Error.Conflict("Products.Create.CodeAlreadyExists", "A product with the same code already exists."));
        }

        if (await _productRepository.SkuExistsAsync(
                _currentUserService.CompanyId,
                normalizedSku,
                cancellationToken))
        {
            return Result<CreateProductResponse>.Failure(
                Error.Conflict("Products.Create.SkuAlreadyExists", "A product with the same SKU already exists."));
        }

        Product product;
        var resolvedPublicPriceResult = ResolvePublicPrice(request.Price, request.PublicPrice);
        if (!resolvedPublicPriceResult.IsSuccess)
        {
            return Result<CreateProductResponse>.Failure(resolvedPublicPriceResult.Error!);
        }

        try
        {
            product = Product.Create(
                _currentUserService.CompanyId,
                request.Code,
                request.Sku,
                request.Brand,
                request.Name,
                request.Description,
                resolvedPublicPriceResult.Value,
                request.CostPrice,
                request.UnitPrice);
        }
        catch (ArgumentException ex)
        {
            return Result<CreateProductResponse>.Failure(
                Error.Validation("Products.Create.InvalidInput", ex.Message));
        }

        await _productRepository.AddAsync(product, cancellationToken);

        var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);
        if (onboarding is null)
        {
            onboarding = CompanyOnboarding.CreateCompleted(_currentUserService.CompanyId);
            await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
        }

        onboarding.MarkProductCreated();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateProductResponse>.Success(
            new CreateProductResponse(
                product.Id.Value,
                product.Code,
                product.Sku,
                product.Brand,
                product.Name,
                product.Description,
                product.Price,
                product.Price,
                product.CostPrice,
                product.UnitPrice,
                0,
                0,
                0,
                product.CreatedAt));
    }

    private static Result<decimal> ResolvePublicPrice(decimal? legacyPrice, decimal? publicPrice)
    {
        if (legacyPrice.HasValue && publicPrice.HasValue && legacyPrice.Value != publicPrice.Value)
        {
            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Create.PriceConflict",
                    "When both price and public price are provided, they must be equal."));
        }

        var resolved = publicPrice ?? legacyPrice;

        if (!resolved.HasValue)
        {
            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Create.PublicPriceRequired",
                    "Either price or public price is required."));
        }

        return Result<decimal>.Success(resolved.Value);
    }
}
