namespace eiti.Application.Features.Stock.Commands.TransferStock;

public sealed record TransferStockResponse(
    Guid SourceBranchId,
    Guid DestinationBranchId,
    IReadOnlyList<TransferStockResultItem> Items);

public sealed record TransferStockResultItem(
    Guid ProductId,
    string Code,
    string Name,
    int Quantity,
    int SourceOnHandQuantity,
    int SourceAvailableQuantity);
