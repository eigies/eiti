using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.WholesaleByCustomer;

// Reporte de ventas mayoristas por cliente. "Mayorista" = venta por Cuenta Corriente (IsCuentaCorriente),
// como lo define el resto del sistema. SaleType default "wholesale" pero editable (retail / all).
public sealed record WholesaleByCustomerQuery(
    DateTime DateFrom,
    DateTime DateTo,
    string SaleType = "wholesale",
    Guid? BranchId = null,
    Guid? CustomerId = null
) : IRequest<Result<WholesaleByCustomerResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsWholesaleByCustomer];
}
