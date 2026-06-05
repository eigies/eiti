namespace eiti.Application.Features.Reports.Queries.StockMatrix;

public sealed record StockMatrixResponse(
    IReadOnlyList<StockMatrixBranch> Branches,
    IReadOnlyList<StockMatrixRow> Rows);

public sealed record StockMatrixBranch(
    Guid Id,
    string Name);

public sealed record StockMatrixRow(
    Guid ProductId,
    string Code,
    string Brand,
    string Name,
    // Disponible por sucursal, alineado con el orden de Branches
    IReadOnlyList<int> Available,
    int Total);
