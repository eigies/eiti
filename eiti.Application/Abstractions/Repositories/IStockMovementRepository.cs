using eiti.Domain.Branches;
using eiti.Domain.Companies;
using eiti.Domain.Products;
using eiti.Domain.Stock;

namespace eiti.Application.Abstractions.Repositories;

public interface IStockMovementRepository
{
    Task AddAsync(
        StockMovement movement,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListAsync(
        BranchId branchId,
        ProductId productId,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListByReferenceAsync(
        Guid referenceId,
        string referenceType,
        CompanyId companyId,
        CancellationToken cancellationToken = default);

    // Movimientos para el reporte por modelo y fecha. Producto/sucursal/tipo opcionales.
    Task<IReadOnlyList<StockMovement>> ListForReportAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? productId,
        Guid? branchId,
        int? type,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);

    // Página de movimientos del reporte (mismos filtros, orden fecha desc).
    Task<IReadOnlyList<StockMovement>> ListForReportPagedAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? productId,
        Guid? branchId,
        int? type,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    // Totales agregados del set completo (sin materializar filas): cantidad por tipo + count total.
    Task<IReadOnlyList<StockMovementTypeAggregate>> GetReportAggregatesAsync(
        CompanyId companyId,
        DateTime from,
        DateTime to,
        Guid? productId,
        Guid? branchId,
        int? type,
        IReadOnlyCollection<Guid>? allowedBranchIds,
        CancellationToken cancellationToken = default);
}

// Agregado por tipo de movimiento para calcular entradas/salidas/neto sin traer todas las filas.
public sealed record StockMovementTypeAggregate(int Type, int Quantity, int Count);
