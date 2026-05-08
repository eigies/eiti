namespace eiti.Application.Features.Products.Commands.ImportProducts;

public sealed record ImportProductsResponse(
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    int ErrorCount,
    IReadOnlyList<ImportProductsRowResponse> Rows);

public sealed record ImportProductsRowResponse(
    int RowNumber,
    string Code,
    string Action,
    string Message);
