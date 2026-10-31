using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.DiscountsReport;

// Ventas con descuento general del período (minoristas y CC), para auditar qué se descontó, quién y por qué.
// UserId filtra por quién fijó el descuento.
public sealed record DiscountsReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null,
    Guid? UserId = null
) : IRequest<Result<DiscountsReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsSalesDiscounts];
}
