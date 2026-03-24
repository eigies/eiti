using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed class CreateCcSaleHandler : IRequestHandler<CreateCcSaleCommand, Result<CreateCcSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCcSaleHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateCcSaleResponse>> Handle(CreateCcSaleCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CreateCcSaleResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.Unauthorized);
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.BranchNotFound);
        }

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), companyId, cancellationToken);
        if (customer is null)
        {
            return Result<CreateCcSaleResponse>.Failure(CreateCcSaleErrors.CustomerNotFound);
        }

        var groupedDetails = request.Details
            .GroupBy(detail => detail.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitPrice = group.FirstOrDefault(i => i.UnitPrice.HasValue)?.UnitPrice,
                DiscountPercent = group.First().DiscountPercent
            })
            .ToList();

        var productMap = new Dictionary<Guid, Product>();
        var saleDetails = new List<SaleDetail>();
        var stockMap = new Dictionary<Guid, BranchProductStock>();

        foreach (var detail in groupedDetails)
        {
            var product = await _productRepository.GetByIdAsync(
                new ProductId(detail.ProductId),
                companyId,
                cancellationToken);

            if (product is null)
            {
                return Result<CreateCcSaleResponse>.Failure(
                    Error.NotFound("Sales.CreateCc.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                branch.Id,
                product.Id,
                companyId,
                cancellationToken);

            stockMap[product.Id.Value] = stock;
            decimal unitPrice;
            if (detail.UnitPrice.HasValue &&
                detail.UnitPrice.Value >= 0 &&
                _currentUserService.HasPermission(PermissionCodes.SalesPriceOverride))
            {
                unitPrice = detail.UnitPrice.Value;
            }
            else
            {
                unitPrice = product.Price;
            }
            saleDetails.Add(SaleDetail.Create(product.Id, detail.Quantity, unitPrice, detail.DiscountPercent));
        }

        foreach (var detail in groupedDetails)
        {
            var stock = stockMap[detail.ProductId];

            try
            {
                stock.Reserve(detail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<CreateCcSaleResponse>.Failure(
                    Error.Validation("Sales.CreateCc.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result<CreateCcSaleResponse>.Failure(
                    Error.Conflict("Sales.CreateCc.StockUnavailable", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId,
                    branch.Id,
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.Reserve,
                    detail.Quantity,
                    "Sale",
                    null,
                    "Stock reserved for CC sale.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        var branchSaleCount = await _saleRepository.CountByBranchAsync(branch.Id, cancellationToken);
        var codePrefix = !string.IsNullOrWhiteSpace(branch.Code)
            ? branch.Code.ToUpper()
            : branch.Name.ToUpper()[..Math.Min(3, branch.Name.Length)];
        var saleCode = $"{codePrefix}-{(branchSaleCount + 1).ToString().PadLeft(3, '0')}";

        Sale sale;

        try
        {
            sale = Sale.CreateCc(
                companyId,
                branch.Id,
                customer.Id,
                saleDetails,
                code: saleCode,
                generalDiscountPercent: request.GeneralDiscountPercent);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<CreateCcSaleResponse>.Failure(
                Error.Validation("Sales.CreateCc.InvalidInput", ex.Message));
        }

        if (request.ManualOverridePrice.HasValue)
        {
            sale.SetManualOverride(request.ManualOverridePrice.Value, _currentUserService.UserId?.Value);
        }

        await _saleRepository.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateCcSaleResponse>.Success(
            new CreateCcSaleResponse(
                sale.Id.Value,
                sale.Code,
                sale.BranchId.Value,
                customer.Id.Value,
                customer.FullName,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.GeneralDiscountPercent,
                sale.OriginalTotal,
                sale.TotalAmount,
                sale.ManualOverridePrice,
                sale.IsCuentaCorriente,
                sale.CreatedAt,
                sale.Details.Select(detail => new CreateCcSaleDetailItemResponse(
                    detail.ProductId.Value,
                    GetProductName(productMap, detail.ProductId.Value),
                    GetProductBrand(productMap, detail.ProductId.Value),
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.DiscountPercent,
                    detail.TotalAmount)).ToList()));
    }

    private static string GetProductName(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product)
            ? product.Name
            : "Deleted product";
    }

    private static string GetProductBrand(IDictionary<Guid, Product> productMap, Guid productId)
    {
        return productMap.TryGetValue(productId, out var product)
            ? product.Brand
            : "Unknown";
    }
}
