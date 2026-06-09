namespace eiti.Application.Features.Reports.Queries.DailySalesControl;

public sealed record DailySalesControlResponse(
    IReadOnlyList<DailySalesControlRow> Rows,
    DailySalesControlTotals Totals);

public sealed record DailySalesControlRow(
    Guid SaleId,
    string? Code,
    DateTime CreatedAt,
    Guid BranchId,
    string BranchName,
    Guid? CustomerId,
    string CustomerName,
    int StatusCode,
    string Status,
    bool IsCuentaCorriente,
    decimal TotalAmount,
    IReadOnlyList<DailySalesProductItem> Products,
    IReadOnlyList<DailySalesPaymentItem> Payments,
    IReadOnlyList<DailySalesTradeInItem> TradeIns);

public sealed record DailySalesProductItem(
    Guid ProductId,
    string Code,
    string Brand,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

public sealed record DailySalesPaymentItem(
    int MethodCode,
    string Method,
    decimal Amount,
    string? Reference);

public sealed record DailySalesTradeInItem(
    Guid ProductId,
    string Code,
    string Brand,
    string Name,
    int Quantity,
    decimal Amount);

public sealed record DailySalesControlTotals(
    int SalesCount,
    int UnitsSold,
    int SalesWithTradeIn,
    decimal TotalAmount);
