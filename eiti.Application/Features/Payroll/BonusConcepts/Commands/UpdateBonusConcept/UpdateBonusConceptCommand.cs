using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.BonusConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.BonusConcepts.Commands.UpdateBonusConcept;

public sealed record UpdateBonusConceptCommand(Guid Id, string Name)
    : IRequest<Result<BonusConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
