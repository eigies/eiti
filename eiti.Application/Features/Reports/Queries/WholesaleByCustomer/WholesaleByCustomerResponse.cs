namespace eiti.Application.Features.Reports.Queries.WholesaleByCustomer;

// Operations = cantidad de operaciones (ventas/tickets) del cliente en el período.
// CcPending = saldo de Cuenta Corriente pendiente ACTUAL del cliente (todas sus ventas CC, no solo el período).
// LastSaleAt = la venta más reciente del cliente dentro del período y los filtros (instante UTC).
public sealed record WholesaleByCustomerResponse(
    string SaleType,
    IReadOnlyList<WholesaleByCustomerRow> Rows,
    WholesaleByCustomerTotals Totals);

public sealed record WholesaleByCustomerRow(
    Guid? CustomerId,
    string CustomerName,
    int Operations,
    int Units,
    decimal Revenue,
    decimal AvgTicket,
    decimal CcPending,
    decimal Cost,
    decimal Profit,
    decimal MarginPct,
    DateTime LastSaleAt);

public sealed record WholesaleByCustomerTotals(
    int Operations,
    int Units,
    decimal Revenue,
    decimal AvgTicket,
    decimal CcPending,
    decimal Cost,
    decimal Profit,
    decimal MarginPct);
