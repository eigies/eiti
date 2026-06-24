using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.PaymentMethodsReport;

public sealed record PaymentMethodsReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid? BranchId = null
) : IRequest<Result<PaymentMethodsReportResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.ReportsPayments];
}
