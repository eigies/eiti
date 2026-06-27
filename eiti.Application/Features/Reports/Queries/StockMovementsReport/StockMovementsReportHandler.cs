using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.StockMovementsReport;

public sealed class StockMovementsReportHandler
    : IRequestHandler<StockMovementsReportQuery, Result<StockMovementsReportResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IPurchaseRepository _purchaseRepository;

    public StockMovementsReportHandler(
        ICurrentUserService currentUserService,
        IStockMovementRepository stockMovementRepository,
        IProductRepository productRepository,
        IBranchRepository branchRepository,
        ISaleRepository saleRepository,
        IPurchaseRepository purchaseRepository)
    {
        _currentUserService = currentUserService;
        _stockMovementRepository = stockMovementRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _saleRepository = saleRepository;
        _purchaseRepository = purchaseRepository;
    }

    public async Task<Result<StockMovementsReportResponse>> Handle(
        StockMovementsReportQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<StockMovementsReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var from = request.DateFrom.Date;
        var to = request.DateTo.Date.AddDays(1).AddTicks(-1);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        var movements = await _stockMovementRepository.ListForReportAsync(
            companyId, from, to, request.ProductId, request.BranchId, request.Type, allowedBranchIds, cancellationToken);

        // Diccionarios de apoyo (nombres) y códigos de documento (batch, sin N+1).
        var products = (await _productRepository.GetByCompanyIdAsync(companyId, cancellationToken))
            .ToDictionary(p => p.Id.Value, p => (p.Code, p.Brand, p.Name));
        var branches = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken) ?? [])
            .ToDictionary(b => b.Id.Value, b => b.Name);

        var saleIds = movements.Where(m => m.ReferenceType == "Sale" && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value).Distinct().ToList();
        var purchaseIds = movements.Where(m => m.ReferenceType == "Purchase" && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value).Distinct().ToList();
        var saleCodes = saleIds.Count > 0
            ? await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken)
            : new Dictionary<Guid, string?>();
        var purchaseCodes = purchaseIds.Count > 0
            ? await _purchaseRepository.GetCodesByPurchaseIdsAsync(purchaseIds, cancellationToken)
            : new Dictionary<Guid, string?>();

        string? ResolveCode(StockMovement m) => !m.ReferenceId.HasValue ? null : m.ReferenceType switch
        {
            "Sale" => saleCodes.GetValueOrDefault(m.ReferenceId.Value),
            "Purchase" => purchaseCodes.GetValueOrDefault(m.ReferenceId.Value),
            _ => null
        };

        var rows = new List<StockMovementsReportRow>(movements.Count);
        var entradas = 0;
        var salidas = 0;
        foreach (var m in movements)
        {
            products.TryGetValue(m.ProductId.Value, out var prod);
            var direction = Direction(m.Type);
            if (direction > 0) entradas += m.Quantity;
            else if (direction < 0) salidas += m.Quantity;

            rows.Add(new StockMovementsReportRow(
                m.CreatedAt,
                m.BranchId.Value,
                branches.GetValueOrDefault(m.BranchId.Value, "—"),
                m.ProductId.Value,
                prod.Code ?? "—",
                prod.Brand ?? string.Empty,
                prod.Name ?? "(Producto)",
                (int)m.Type,
                TypeLabel(m.Type),
                direction,
                m.Quantity,
                m.ReferenceType,
                m.ReferenceId,
                ResolveCode(m),
                m.Description));
        }

        var totals = new StockMovementsReportTotals(entradas, salidas, entradas - salidas, movements.Count);
        return Result<StockMovementsReportResponse>.Success(new StockMovementsReportResponse(rows, totals));
    }

    private static int Direction(StockMovementType type) => type switch
    {
        StockMovementType.ManualEntry => 1,
        StockMovementType.SaleReturn => 1,
        StockMovementType.PurchaseIn => 1,
        StockMovementType.TransferIn => 1,
        StockMovementType.TradeInIn => 1,
        StockMovementType.SaleOut => -1,
        StockMovementType.PurchaseReturn => -1,
        StockMovementType.TransferOut => -1,
        _ => 0 // ManualAdjustment / Reserve / ReleaseReservation: neutro para el neto de stock físico
    };

    private static string TypeLabel(StockMovementType type) => type switch
    {
        StockMovementType.ManualEntry => "Entrada manual",
        StockMovementType.ManualAdjustment => "Ajuste manual",
        StockMovementType.Reserve => "Reserva",
        StockMovementType.ReleaseReservation => "Liberación de reserva",
        StockMovementType.SaleOut => "Venta",
        StockMovementType.TradeInIn => "Canje (ingreso)",
        StockMovementType.SaleReturn => "Devolución de venta",
        StockMovementType.PurchaseIn => "Compra",
        StockMovementType.PurchaseReturn => "Devolución de compra",
        StockMovementType.TransferOut => "Transferencia (salida)",
        StockMovementType.TransferIn => "Transferencia (entrada)",
        _ => type.ToString()
    };
}
