using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.ListStockMovements;

public sealed class ListStockMovementsHandler : IRequestHandler<ListStockMovementsQuery, Result<IReadOnlyList<StockMovementResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    public ListStockMovementsHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<Result<IReadOnlyList<StockMovementResponse>>> Handle(ListStockMovementsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<StockMovementResponse>>.Failure(
                Error.Unauthorized("Stock.Movements.Unauthorized", "The current user is not authenticated."));
        }

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

        return Result<IReadOnlyList<StockMovementResponse>>.Success(
            movements.Select(movement => new StockMovementResponse(
                movement.Id.Value,
                (int)movement.Type,
                movement.Type.ToString(),
                movement.Quantity,
                movement.ReferenceType,
                movement.ReferenceId,
                movement.Description,
                movement.CreatedAt)).ToList());
    }
}
