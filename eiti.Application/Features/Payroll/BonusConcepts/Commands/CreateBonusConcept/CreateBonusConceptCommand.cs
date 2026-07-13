using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.CreateBonusConcept;

public sealed record CreateBonusConceptCommand(string Name)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
