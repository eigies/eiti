using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.TransferStock;

public sealed class TransferStockHandler : IRequestHandler<TransferStockCommand, Result<TransferStockResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TransferStockHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransferStockResponse>> Handle(TransferStockCommand request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<TransferStockResponse>.Failure(authCheck.Error);

        if (request.SourceBranchId == request.DestinationBranchId)
            return Result<TransferStockResponse>.Failure(TransferStockErrors.SameBranch);

        // La sucursal como paraguas: el usuario debe tener acceso TANTO al origen como al destino
        // (ambos entre sus sucursales asignadas, o tener "ver todas"). No necesita ver todas.
        var sourceAccess = _currentUserService.EnsureBranchAccess(request.SourceBranchId);
        if (sourceAccess.IsFailure)
            return Result<TransferStockResponse>.Failure(sourceAccess.Error);

        var destinationAccess = _currentUserService.EnsureBranchAccess(request.DestinationBranchId);
        if (destinationAccess.IsFailure)
            return Result<TransferStockResponse>.Failure(destinationAccess.Error);

        if (request.Items is null || request.Items.Count == 0)
            return Result<TransferStockResponse>.Failure(TransferStockErrors.NoItems);

        if (request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Count)
            return Result<TransferStockResponse>.Failure(TransferStockErrors.DuplicateProduct);

        var companyId = _currentUserService.CompanyId;

        var sourceBranch = await _branchRepository.GetByIdAsync(new BranchId(request.SourceBranchId), companyId, cancellationToken);
        if (sourceBranch is null)
            return Result<TransferStockResponse>.Failure(TransferStockErrors.SourceBranchNotFound);

        var destinationBranch = await _branchRepository.GetByIdAsync(new BranchId(request.DestinationBranchId), companyId, cancellationToken);
        if (destinationBranch is null)
            return Result<TransferStockResponse>.Failure(TransferStockErrors.DestinationBranchNotFound);

        // Primera pasada: cargar productos + stock de origen y validar disponibilidad de TODO antes de aplicar nada.
        var prepared = new List<(Product Product, BranchProductStock SourceStock, BranchProductStock DestinationStock, int Quantity)>();

        foreach (var item in request.Items)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(item.ProductId), companyId, cancellationToken);
            if (product is null)
                return Result<TransferStockResponse>.Failure(TransferStockErrors.ProductNotFound(item.ProductId));

            var sourceStock = await _branchProductStockRepository.GetOrCreateAsync(sourceBranch.Id, product.Id, companyId, cancellationToken);
            if (sourceStock.AvailableQuantity < item.Quantity)
                return Result<TransferStockResponse>.Failure(
                    TransferStockErrors.InsufficientStock(product.Name, sourceStock.AvailableQuantity, item.Quantity));

            var destinationStock = await _branchProductStockRepository.GetOrCreateAsync(destinationBranch.Id, product.Id, companyId, cancellationToken);

            prepared.Add((product, sourceStock, destinationStock, item.Quantity));
        }

        // Segunda pasada: aplicar movimientos. Todo se persiste en un único SaveChanges (atómico).
        var note = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
        var transferReferenceId = Guid.NewGuid();
        var resultItems = new List<TransferStockResultItem>(prepared.Count);

        foreach (var (product, sourceStock, destinationStock, quantity) in prepared)
        {
            try
            {
                sourceStock.ApplyTransferOut(quantity);
                destinationStock.ApplyTransferIn(quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<TransferStockResponse>.Failure(
                    Error.Validation("Stock.Transfer.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException)
            {
                return Result<TransferStockResponse>.Failure(
                    TransferStockErrors.InsufficientStock(product.Name, sourceStock.AvailableQuantity, quantity));
            }

            var outDescription = Truncate(BuildDescription($"Transferencia a {destinationBranch.Name}", note), 255);
            var inDescription = Truncate(BuildDescription($"Transferencia desde {sourceBranch.Name}", note), 255);

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId, sourceBranch.Id, product.Id, sourceStock.Id,
                    StockMovementType.TransferOut, quantity,
                    "Transfer", transferReferenceId, outDescription, _currentUserService.UserId),
                cancellationToken);

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    companyId, destinationBranch.Id, product.Id, destinationStock.Id,
                    StockMovementType.TransferIn, quantity,
                    "Transfer", transferReferenceId, inDescription, _currentUserService.UserId),
                cancellationToken);

            resultItems.Add(new TransferStockResultItem(
                product.Id.Value,
                product.Code,
                product.Name,
                quantity,
                sourceStock.OnHandQuantity,
                sourceStock.AvailableQuantity));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TransferStockResponse>.Success(
            new TransferStockResponse(sourceBranch.Id.Value, destinationBranch.Id.Value, resultItems));
    }

    private static string BuildDescription(string prefix, string? note) =>
        note is null ? prefix : $"{prefix} - {note}";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
