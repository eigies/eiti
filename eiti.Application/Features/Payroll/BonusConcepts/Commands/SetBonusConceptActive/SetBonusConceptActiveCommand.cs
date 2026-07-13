using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.SetBonusConceptActive;

public sealed record SetBonusConceptActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
