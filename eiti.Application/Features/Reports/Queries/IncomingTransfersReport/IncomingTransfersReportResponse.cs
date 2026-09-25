namespace eiti.Application.Features.Reports.Queries.IncomingTransfersReport;

// BranchScoped: el usuario solo ve algunas sucursales, así que la lista puede estar incompleta
// respecto de una cuenta bancaria que es de toda la empresa.
// UnassignedCount/Total: transferencias del período sin banco receptor cargado (solo se informan
// al filtrar por banco, porque en ese caso quedan afuera de Rows).
public sealed record IncomingTransfersReportResponse(
    IReadOnlyList<IncomingTransferRow> Rows,
    bool BranchScoped = false,
    int UnassignedCount = 0,
    decimal UnassignedTotal = 0m);

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
    string? BranchName,
    bool DateOnly = false);  // OccurredAt es solo un día (cobro CC cargado con fecha, sin hora)
