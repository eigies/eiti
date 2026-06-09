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
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IProductCategoryRepository categoryRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateProductResponse>> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateProductResponse>.Failure(authCheck.Error);

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

        // Categoría opcional: si viene, debe existir en la empresa.
        string? categoryName = null;
        if (request.CategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(
                request.CategoryId.Value, _currentUserService.CompanyId!.Value, cancellationToken);
            if (category is null)
                return Result<CreateProductResponse>.Failure(
                    Error.Validation("Products.Create.CategoryNotFound", "La categoría seleccionada no existe."));
            categoryName = category.Name;
        }

        Product product;
        var resolvedPublicPriceResult = ResolvePublicPrice(
            request.Price,
            request.PublicPrice,
            request.AllowsManualValueInSale);
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
                request.UnitPrice,
                request.AllowsManualValueInSale,
                request.NoDeliverySurcharge,
                request.CategoryId);
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
                product.AllowsManualValueInSale,
                product.NoDeliverySurcharge,
                product.CategoryId,
                categoryName,
                0,
                0,
                0,
                product.CreatedAt));
    }

    private static Result<decimal> ResolvePublicPrice(
        decimal? legacyPrice,
        decimal? publicPrice,
        bool allowsManualValueInSale)
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

        if (!allowsManualValueInSale && resolved.Value <= 0)
        {
            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Create.PublicPriceMustBePositive",
                    "Public price must be greater than zero unless manual value in sale is allowed."));
        }

        return Result<decimal>.Success(resolved.Value);
    }
}
