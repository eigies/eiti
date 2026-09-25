namespace eiti.Application.Features.Reports.Queries.IncomingTransfersReport;

public sealed record IncomingTransfersReportResponse(IReadOnlyList<IncomingTransferRow> Rows);

// Source: "sale" (pago por transferencia de una venta minorista) | "cc_collection" (cobro de cuenta corriente).
// OccurredAt: instante UTC más preciso disponible (el movimiento de caja del cobro, si existe).
public sealed record IncomingTransferRow(
    DateTime OccurredAt,
    decimal Amount,
    string Source,
    int? BankId,
    string? BankName,
    string? Reference,
    string? CustomerName,
    string? BranchName);
