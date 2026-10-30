namespace eiti.Application.Features.Reports.Queries.DiscountsReport;

public sealed record DiscountsReportResponse(
    IReadOnlyList<DiscountsReportRow> Rows,
    DiscountsReportTotals Totals);

// SubtotalBeforeDiscount = renglones + recargo sin envío (Sale.OriginalTotal).
// DiscountAmount = lo que restó el porcentaje, sin contar un precio manual (override).
// Total = el total de la venta (con override, si lo tiene).
// AppliedByUserId/Name en null: venta anterior a la traza del descuento.
public sealed record DiscountsReportRow(
    Guid SaleId,
    string? SaleCode,
    DateTime Date,
    string BranchName,
    string CustomerName,
    bool IsCuentaCorriente,
    Guid? AppliedByUserId,
    string? AppliedByUserName,
    decimal DiscountPercent,
    decimal SubtotalBeforeDiscount,
    decimal DiscountAmount,
    decimal Total,
    string? Reason);

public sealed record DiscountsReportTotals(
    int SalesCount,
    decimal DiscountAmount);
