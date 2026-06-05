using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Stock.Queries.GetTransferDetail;

public sealed class GetTransferDetailHandler : IRequestHandler<GetTransferDetailQuery, Result<TransferDetailResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;

    public GetTransferDetailHandler(
        ICurrentUserService currentUserService,
        IStockMovementRepository stockMovementRepository,
        IBranchRepository branchRepository,
        IProductRepository productRepository)
    {
        _currentUserService = currentUserService;
        _stockMovementRepository = stockMovementRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<TransferDetailResponse>> Handle(GetTransferDetailQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<TransferDetailResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;

        var movements = await _stockMovementRepository.ListByReferenceAsync(
            request.ReferenceId, "Transfer", companyId, cancellationToken);

        if (movements.Count == 0)
            return Result<TransferDetailResponse>.Failure(
                Error.NotFound("Stock.Transfer.NotFound", "El traspaso no fue encontrado."));

        var outLegs = movements.Where(m => m.Type == StockMovementType.TransferOut).ToList();
        var inLegs = movements.Where(m => m.Type == StockMovementType.TransferIn).ToList();

        var fromBranchId = outLegs.FirstOrDefault()?.BranchId.Value;
        var toBranchId = inLegs.FirstOrDefault()?.BranchId.Value;

        async Task<string?> BranchName(Guid? branchId)
        {
            if (branchId is null) return null;
            var branch = await _branchRepository.GetByIdAsync(new BranchId(branchId.Value), companyId, cancellationToken);
            return branch?.Name;
        }

        var fromBranchName = await BranchName(fromBranchId);
        var toBranchName = await BranchName(toBranchId);

        // Ítems = patas de salida (una por producto). Si no hubiera, usar las de entrada.
        var itemLegs = outLegs.Count > 0 ? outLegs : inLegs;
        var items = new List<TransferDetailItem>(itemLegs.Count);
        foreach (var leg in itemLegs)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(leg.ProductId.Value), companyId, cancellationToken);
            items.Add(new TransferDetailItem(
                product?.Code ?? "—",
                product?.Brand ?? string.Empty,
                product?.Name ?? "(Producto)",
                leg.Quantity));
        }

        return Result<TransferDetailResponse>.Success(
            new TransferDetailResponse(
                request.ReferenceId,
                movements.Min(m => m.CreatedAt),
                fromBranchId,
                fromBranchName,
                toBranchId,
                toBranchName,
                movements.FirstOrDefault()?.Description,
                items));
    }
}
