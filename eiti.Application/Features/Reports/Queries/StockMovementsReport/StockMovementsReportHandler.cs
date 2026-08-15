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

        // El rango llega como fecha local del usuario; se traduce al instante UTC equivalente.
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : request.PageSize;

        // Totales y count sobre el set COMPLETO, agregados en la DB (sin materializar todas las filas).
        var aggregates = await _stockMovementRepository.GetReportAggregatesAsync(
            companyId, from, to, request.ProductId, request.BranchId, request.Type, allowedBranchIds, cancellationToken);

        var entradas = 0;
        var salidas = 0;
        var totalCount = 0;
        foreach (var agg in aggregates)
        {
            totalCount += agg.Count;
            var direction = Direction((StockMovementType)agg.Type);
            if (direction > 0) entradas += agg.Quantity;
            else if (direction < 0) salidas += agg.Quantity;
        }

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        // Solo la página pedida.
        var movements = await _stockMovementRepository.ListForReportPagedAsync(
            companyId, from, to, request.ProductId, request.BranchId, request.Type, allowedBranchIds,
            (page - 1) * pageSize, pageSize, cancellationToken);

        // Nombres SOLO de los productos presentes en la página (evita cargar todo el catálogo).
        var pageProductIds = movements.Select(m => m.ProductId).Distinct().ToList();
        var products = pageProductIds.Count > 0
            ? (await _productRepository.GetByIdsAsync(pageProductIds, companyId, cancellationToken))
                .ToDictionary(p => p.Id.Value, p => (p.Code, p.Brand, p.Name))
            : new Dictionary<Guid, (string Code, string Brand, string Name)>();
        var branches = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken) ?? [])
            .ToDictionary(b => b.Id.Value, b => b.Name);

        // Códigos de documento (batch, sin N+1) solo para la página.
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
        foreach (var m in movements)
        {
            products.TryGetValue(m.ProductId.Value, out var prod);
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
                Direction(m.Type),
                m.Quantity,
                m.ReferenceType,
                m.ReferenceId,
                ResolveCode(m),
                m.Description));
        }

        var totals = new StockMovementsReportTotals(entradas, salidas, entradas - salidas, totalCount);
        return Result<StockMovementsReportResponse>.Success(
            new StockMovementsReportResponse(rows, totals, page, pageSize, totalCount, totalPages));
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
