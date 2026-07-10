using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CancelPayrollAdvance;

public sealed record CancelPayrollAdvanceCommand(Guid Id) : IRequest<Result<PayrollAdvanceResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollAdvancesManage];
}
