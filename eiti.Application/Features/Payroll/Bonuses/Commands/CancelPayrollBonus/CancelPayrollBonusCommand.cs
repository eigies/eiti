using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CancelPayrollBonus;

public sealed record CancelPayrollBonusCommand(Guid Id) : IRequest<Result<PayrollBonusResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
