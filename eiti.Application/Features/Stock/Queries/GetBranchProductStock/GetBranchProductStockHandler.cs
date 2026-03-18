using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetBranchProductStock;

public sealed class GetBranchProductStockHandler : IRequestHandler<GetBranchProductStockQuery, Result<BranchProductStockResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;

    public GetBranchProductStockHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
    }

    public async Task<Result<BranchProductStockResponse>> Handle(GetBranchProductStockQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<BranchProductStockResponse>.Failure(authCheck.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);
        if (branch is null)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.NotFound("Stock.Get.BranchNotFound", "The selected branch was not found."));
        }

        var product = await _productRepository.GetByIdAsync(new ProductId(request.ProductId), _currentUserService.CompanyId, cancellationToken);
        if (product is null)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.NotFound("Stock.Get.ProductNotFound", "The selected product was not found."));
        }

        var stock = await _branchProductStockRepository.GetByBranchAndProductAsync(branch.Id, product.Id, _currentUserService.CompanyId, cancellationToken);

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
                stock?.OnHandQuantity ?? 0,
                stock?.ReservedQuantity ?? 0,
                stock?.AvailableQuantity ?? 0,
                stock?.UpdatedAt));
    }
}
