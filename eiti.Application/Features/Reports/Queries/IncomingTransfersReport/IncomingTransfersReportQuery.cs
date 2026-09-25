using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.IncomingTransfersReport;

// Transferencias entrantes individuales (pagos de venta y cobros de cuenta corriente) para
// conciliarlas contra el extracto del banco o billetera que las recibió.
public sealed record IncomingTransfersReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    int? BankId = null,
    Guid? BranchId = null
) : IRequest<Result<IncomingTransfersReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsPayments];
}
