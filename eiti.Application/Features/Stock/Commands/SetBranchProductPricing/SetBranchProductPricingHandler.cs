using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.SetBranchProductPricing;

public sealed class SetBranchProductPricingHandler
    : IRequestHandler<SetBranchProductPricingCommand, Result<BranchProductStockResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetBranchProductPricingHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BranchProductStockResponse>> Handle(
        SetBranchProductPricingCommand request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BranchProductStockResponse>.Failure(authCheck.Error);

        var branchAccess = _currentUserService.EnsureBranchAccess(request.BranchId);
        if (branchAccess.IsFailure)
            return Result<BranchProductStockResponse>.Failure(branchAccess.Error);

        var companyId = _currentUserService.CompanyId;

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), companyId, cancellationToken);
        if (branch is null)
            return Result<BranchProductStockResponse>.Failure(SetBranchProductPricingErrors.BranchNotFound);

        var product = await _productRepository.GetByIdAsync(new ProductId(request.ProductId), companyId!, cancellationToken);
        if (product is null)
            return Result<BranchProductStockResponse>.Failure(SetBranchProductPricingErrors.ProductNotFound);

        var stock = await _branchProductStockRepository.GetOrCreateAsync(branch.Id, product.Id, companyId!, cancellationToken);

        try
        {
            stock.SetPricing(request.CostOverride, request.SalePriceOverride);
        }
        catch (ArgumentException)
        {
            return Result<BranchProductStockResponse>.Failure(SetBranchProductPricingErrors.InvalidPricing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BranchProductStockResponse>.Success(
            new BranchProductStockResponse(
                product.Id.Value,
                branch.Id.Value,
                product.Code,
                product.Sku,
                product.Brand,
                product.Name,
                product.Price,
                product.Price,
                product.CostPrice,
                product.UnitPrice,
                product.AllowsManualValueInSale,
                stock.OnHandQuantity,
                stock.ReservedQuantity,
                stock.AvailableQuantity,
                stock.UpdatedAt,
                stock.CostOverride,
                stock.SalePriceOverride,
                BranchPricing.ResolvePrice(stock, product),
                BranchPricing.ResolveCost(stock, product)));
    }
}
