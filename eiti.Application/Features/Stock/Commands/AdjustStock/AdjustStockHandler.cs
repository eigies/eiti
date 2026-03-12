using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Stock.Common;
using eiti.Domain.Companies;
using eiti.Domain.Branches;
using eiti.Domain.Products;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Stock.Commands.AdjustStock;

public sealed class AdjustStockHandler : IRequestHandler<AdjustStockCommand, Result<BranchProductStockResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICompanyOnboardingRepository _companyOnboardingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdjustStockHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICompanyOnboardingRepository companyOnboardingRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _companyOnboardingRepository = companyOnboardingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BranchProductStockResponse>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.Unauthorized("Stock.Adjust.Unauthorized", "The current user is not authenticated."));
        }

        var branch = await _branchRepository.GetByIdAsync(new BranchId(request.BranchId), _currentUserService.CompanyId, cancellationToken);
        if (branch is null)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.NotFound("Stock.Adjust.BranchNotFound", "The selected branch was not found."));
        }

        var product = await _productRepository.GetByIdAsync(new ProductId(request.ProductId), _currentUserService.CompanyId, cancellationToken);
        if (product is null)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.NotFound("Stock.Adjust.ProductNotFound", "The selected product was not found."));
        }

        if (!Enum.IsDefined(typeof(StockMovementType), request.Type))
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.Validation("Stock.Adjust.InvalidType", "The selected stock movement type is invalid."));
        }

        var movementType = (StockMovementType)request.Type;
        if (movementType is not (StockMovementType.ManualEntry or StockMovementType.ManualAdjustment))
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.Validation("Stock.Adjust.InvalidManualType", "Only manual stock entries and manual adjustments are allowed."));
        }

        var stock = await _branchProductStockRepository.GetOrCreateAsync(branch.Id, product.Id, _currentUserService.CompanyId, cancellationToken);

        try
        {
            if (movementType == StockMovementType.ManualEntry)
            {
                stock.ApplyManualEntry(request.Quantity);
            }
            else
            {
                stock.ApplyManualAdjustment(request.Quantity);
            }
        }
        catch (ArgumentException ex)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.Validation("Stock.Adjust.InvalidQuantity", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<BranchProductStockResponse>.Failure(
                Error.Conflict("Stock.Adjust.InvalidAdjustment", ex.Message));
        }

        var movement = StockMovement.Create(
            _currentUserService.CompanyId,
            branch.Id,
            product.Id,
            stock.Id,
            movementType,
            Math.Abs(request.Quantity),
            null,
            null,
            string.IsNullOrWhiteSpace(request.Description)
                ? movementType == StockMovementType.ManualEntry ? "Manual stock entry" : "Manual stock adjustment"
                : request.Description,
            _currentUserService.UserId);

        await _stockMovementRepository.AddAsync(movement, cancellationToken);

        if (stock.OnHandQuantity > 0)
        {
            var onboarding = await _companyOnboardingRepository.GetByCompanyIdAsync(_currentUserService.CompanyId, cancellationToken);
            if (onboarding is null)
            {
                onboarding = CompanyOnboarding.CreateCompleted(_currentUserService.CompanyId);
                await _companyOnboardingRepository.AddAsync(onboarding, cancellationToken);
            }

            onboarding.MarkInitialStockLoaded();
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
                stock.UpdatedAt));
    }
}
