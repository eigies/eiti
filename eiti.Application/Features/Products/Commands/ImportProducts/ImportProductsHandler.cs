using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Products.Commands.ImportProducts;

public sealed class ImportProductsHandler
    : IRequestHandler<ImportProductsCommand, Result<ImportProductsResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _categoryRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportProductsHandler(
        ICurrentUserService currentUserService,
        IProductRepository productRepository,
        IProductCategoryRepository categoryRepository,
        IBranchRepository branchRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _branchRepository = branchRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ImportProductsResponse>> Handle(
        ImportProductsCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
        {
            return Result<ImportProductsResponse>.Failure(authCheck.Error);
        }

        if (request.Rows.Count == 0)
        {
            return Result<ImportProductsResponse>.Failure(ImportProductsErrors.RowsRequired);
        }

        var companyId = _currentUserService.CompanyId!;
        var products = await _productRepository.GetByCompanyIdAsync(
            companyId,
            cancellationToken);
        var categories = await _categoryRepository.ListByCompanyAsync(companyId.Value, cancellationToken) ?? [];
        var categoriesByName = categories.ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);

        var branches = await _branchRepository.ListByCompanyAsync(companyId, cancellationToken) ?? [];
        var branchesByName = branches.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);

        var productsByCode = products.ToDictionary(product => product.Code, StringComparer.OrdinalIgnoreCase);
        var productsBySku = products.ToDictionary(product => product.Sku, StringComparer.OrdinalIgnoreCase);
        var productsByName = products.ToDictionary(product => product.Name, StringComparer.OrdinalIgnoreCase);

        var duplicateCodes = request.Rows
            .Select(row => NormalizeCodeKey(row.Code))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var createdCount = 0;
        var updatedCount = 0;
        var rowResults = new List<ImportProductsRowResponse>(request.Rows.Count);

        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var rowNumber = index + 2;
            var code = row.Code ?? string.Empty;
            var sku = row.Sku ?? string.Empty;
            var brand = row.Brand ?? string.Empty;
            var name = row.Name ?? string.Empty;
            var rawCode = code.Trim();
            var normalizedCode = NormalizeCodeKey(code);
            Guid? importedCategoryId = null;

            if (!string.IsNullOrWhiteSpace(row.CategoryName))
            {
                if (!categoriesByName.TryGetValue(row.CategoryName.Trim(), out var category))
                {
                    rowResults.Add(ErrorRow(rowNumber, rawCode, $"Category '{row.CategoryName}' not found."));
                    continue;
                }

                importedCategoryId = category.Id;
            }

            if (!string.IsNullOrWhiteSpace(normalizedCode)
                && duplicateCodes.Contains(normalizedCode))
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, "Duplicate code found in the same import file."));
                continue;
            }

            var publicPriceResult = ResolvePublicPrice(row.PublicPrice, row.AllowsManualValueInSale);
            if (publicPriceResult.IsFailure)
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, publicPriceResult.Error.Description));
                continue;
            }

            if (row.NoDeliverySurcharge.HasValue && row.NoDeliverySurcharge.Value < 0)
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, "No delivery surcharge cannot be negative."));
                continue;
            }

            if (productsByCode.TryGetValue(normalizedCode, out var existingProduct))
            {
                var skuOwner = FindBySku(productsBySku, sku);
                if (skuOwner is not null && skuOwner.Id != existingProduct.Id)
                {
                    rowResults.Add(ErrorRow(rowNumber, rawCode, "Another product with the same SKU already exists."));
                    continue;
                }

                var nameOwner = FindByName(productsByName, name);
                if (nameOwner is not null && nameOwner.Id != existingProduct.Id)
                {
                    rowResults.Add(ErrorRow(rowNumber, rawCode, "Another product with the same name already exists."));
                    continue;
                }

                var previousSku = existingProduct.Sku;
                var previousName = existingProduct.Name;

                try
                {
                    existingProduct.Update(
                        code,
                        sku,
                        brand,
                        name,
                        row.Description,
                        publicPriceResult.Value,
                        row.CostPrice,
                        row.UnitPrice,
                        row.AllowsManualValueInSale,
                        row.NoDeliverySurcharge,
                        importedCategoryId ?? existingProduct.CategoryId);
                }
                catch (ArgumentException ex)
                {
                    rowResults.Add(ErrorRow(rowNumber, rawCode, ex.Message));
                    continue;
                }

                productsByCode[existingProduct.Code] = existingProduct;
                ReplaceLookup(productsBySku, previousSku, existingProduct.Sku, existingProduct);
                ReplaceLookup(productsByName, previousName, existingProduct.Name, existingProduct);

                updatedCount++;

                if (!string.IsNullOrWhiteSpace(row.BranchName))
                {
                    if (!branchesByName.TryGetValue(row.BranchName.Trim(), out var branch))
                    {
                        rowResults.Add(ErrorRow(rowNumber, rawCode, $"Branch '{row.BranchName}' not found."));
                        continue;
                    }

                    var quantity = row.InitialStock ?? 0;
                    if (quantity > 0)
                    {
                        var stock = await _branchProductStockRepository.GetOrCreateAsync(branch.Id, existingProduct.Id, companyId, cancellationToken);
                        stock.ApplyManualEntry(quantity);
                        var movement = StockMovement.Create(companyId, branch.Id, existingProduct.Id, stock.Id, StockMovementType.ManualEntry, quantity, null, null, "Import", _currentUserService.UserId);
                        await _stockMovementRepository.AddAsync(movement, cancellationToken);
                    }
                }

                rowResults.Add(SuccessRow(rowNumber, existingProduct.Code, "updated", "Product updated."));
                continue;
            }

            if (FindBySku(productsBySku, sku) is not null)
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, "Another product with the same SKU already exists."));
                continue;
            }

            if (FindByName(productsByName, name) is not null)
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, "A product with that name already exists for the current company."));
                continue;
            }

            Product createdProduct;
            try
            {
                createdProduct = Product.Create(
                    companyId,
                    code,
                    sku,
                    brand,
                    name,
                    row.Description,
                    publicPriceResult.Value,
                    row.CostPrice,
                    row.UnitPrice,
                    row.AllowsManualValueInSale,
                    row.NoDeliverySurcharge,
                    importedCategoryId);
            }
            catch (ArgumentException ex)
            {
                rowResults.Add(ErrorRow(rowNumber, rawCode, ex.Message));
                continue;
            }

            await _productRepository.AddAsync(createdProduct, cancellationToken);
            productsByCode[createdProduct.Code] = createdProduct;
            productsBySku[createdProduct.Sku] = createdProduct;
            productsByName[createdProduct.Name] = createdProduct;

            createdCount++;

            if (!string.IsNullOrWhiteSpace(row.BranchName))
            {
                if (!branchesByName.TryGetValue(row.BranchName.Trim(), out var branch))
                {
                    rowResults.Add(ErrorRow(rowNumber, rawCode, $"Branch '{row.BranchName}' not found."));
                    continue;
                }

                var quantity = row.InitialStock ?? 0;
                if (quantity > 0)
                {
                    var stock = await _branchProductStockRepository.GetOrCreateAsync(branch.Id, createdProduct.Id, companyId, cancellationToken);
                    stock.ApplyManualEntry(quantity);
                    var movement = StockMovement.Create(companyId, branch.Id, createdProduct.Id, stock.Id, StockMovementType.ManualEntry, quantity, null, null, "Import", _currentUserService.UserId);
                    await _stockMovementRepository.AddAsync(movement, cancellationToken);
                }
            }

            rowResults.Add(SuccessRow(rowNumber, createdProduct.Code, "created", "Product created."));
        }

        if (createdCount > 0)
        {
            var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(companyId, cancellationToken);
            if (onboarding is null)
            {
                onboarding = CompanyOnboarding.CreateCompleted(companyId);
                await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
            }

            onboarding.MarkProductCreated();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ImportProductsResponse>.Success(
            new ImportProductsResponse(
                request.Rows.Count,
                createdCount,
                updatedCount,
                rowResults.Count(result => result.Action == "error"),
                rowResults));
    }

    private static Result<decimal> ResolvePublicPrice(decimal? publicPrice, bool allowsManualValueInSale)
    {
        if (!publicPrice.HasValue)
        {
            if (allowsManualValueInSale)
            {
                return Result<decimal>.Success(0m);
            }

            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Import.PublicPriceRequired",
                    "Public price is required."));
        }

        if (!allowsManualValueInSale && publicPrice.Value <= 0)
        {
            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Import.PublicPriceMustBePositive",
                    "Public price must be greater than zero unless manual value in sale is allowed."));
        }

        if (publicPrice.Value < 0)
        {
            return Result<decimal>.Failure(
                Error.Validation(
                    "Products.Import.PublicPriceCannotBeNegative",
                    "Public price cannot be negative."));
        }

        return Result<decimal>.Success(publicPrice.Value);
    }

    private static string NormalizeCodeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string NormalizeSkuKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string NormalizeNameKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static Product? FindBySku(
        IReadOnlyDictionary<string, Product> productsBySku,
        string? sku)
    {
        var normalizedSku = NormalizeSkuKey(sku);
        return normalizedSku.Length > 0 && productsBySku.TryGetValue(normalizedSku, out var product)
            ? product
            : null;
    }

    private static Product? FindByName(
        IReadOnlyDictionary<string, Product> productsByName,
        string? name)
    {
        var normalizedName = NormalizeNameKey(name);
        return normalizedName.Length > 0 && productsByName.TryGetValue(normalizedName, out var product)
            ? product
            : null;
    }

    private static void ReplaceLookup(
        IDictionary<string, Product> lookup,
        string previousKey,
        string nextKey,
        Product product)
    {
        if (!string.Equals(previousKey, nextKey, StringComparison.OrdinalIgnoreCase))
        {
            lookup.Remove(previousKey);
        }

        lookup[nextKey] = product;
    }

    private static ImportProductsRowResponse SuccessRow(
        int rowNumber,
        string code,
        string action,
        string message)
        => new(rowNumber, code, action, message);

    private static ImportProductsRowResponse ErrorRow(
        int rowNumber,
        string code,
        string message)
        => new(rowNumber, code, "error", message);
}
