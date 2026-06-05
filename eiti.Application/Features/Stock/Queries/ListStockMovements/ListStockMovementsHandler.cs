using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.ListStockMovements;

public sealed class ListStockMovementsHandler : IRequestHandler<ListStockMovementsQuery, Result<IReadOnlyList<StockMovementResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IPurchaseRepository _purchaseRepository;

    public ListStockMovementsHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        IPurchaseRepository purchaseRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _purchaseRepository = purchaseRepository;
    }

    public async Task<Result<IReadOnlyList<StockMovementResponse>>> Handle(ListStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<StockMovementResponse>>.Failure(authCheck.Error);

        var branchAccess = _currentUserService.EnsureBranchAccess(request.BranchId);
        if (branchAccess.IsFailure)
            return Result<IReadOnlyList<StockMovementResponse>>.Failure(branchAccess.Error);

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);
        if (branch is null)
        {
            return Result<IReadOnlyList<StockMovementResponse>>.Failure(
                Error.NotFound("Stock.Movements.BranchNotFound", "The selected branch was not found."));
        }

        var product = await _productRepository.GetByIdAsync(new ProductId(request.ProductId), _currentUserService.CompanyId, cancellationToken);
        if (product is null)
        {
            return Result<IReadOnlyList<StockMovementResponse>>.Failure(
                Error.NotFound("Stock.Movements.ProductNotFound", "The selected product was not found."));
        }

        var movements = await _stockMovementRepository.ListAsync(branch.Id, product.Id, _currentUserService.CompanyId, cancellationToken);

        // Resolución batch del código legible del documento (venta/compra) que originó cada movimiento.
        var saleIds = movements
            .Where(m => m.ReferenceType == "Sale" && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value)
            .Distinct()
            .ToList();
        var purchaseIds = movements
            .Where(m => m.ReferenceType == "Purchase" && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value)
            .Distinct()
            .ToList();

        var saleCodes = saleIds.Count > 0
            ? await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken)
            : new Dictionary<Guid, string?>();
        var purchaseCodes = purchaseIds.Count > 0
            ? await _purchaseRepository.GetCodesByPurchaseIdsAsync(purchaseIds, cancellationToken)
            : new Dictionary<Guid, string?>();

        string? ResolveCode(StockMovement movement)
        {
            if (!movement.ReferenceId.HasValue)
                return null;

            return movement.ReferenceType switch
            {
                "Sale" => saleCodes.GetValueOrDefault(movement.ReferenceId.Value),
                "Purchase" => purchaseCodes.GetValueOrDefault(movement.ReferenceId.Value),
                _ => null
            };
        }

        return Result<IReadOnlyList<StockMovementResponse>>.Success(
            movements.Select(movement => new StockMovementResponse(
                movement.Id.Value,
                (int)movement.Type,
                movement.Type.ToString(),
                movement.Quantity,
                movement.ReferenceType,
                movement.ReferenceId,
                movement.Description,
                movement.CreatedAt,
                ResolveCode(movement))).ToList());
    }
}
