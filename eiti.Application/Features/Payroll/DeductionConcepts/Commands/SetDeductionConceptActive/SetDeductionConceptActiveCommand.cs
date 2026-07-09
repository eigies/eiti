using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.SetDeductionConceptActive;

public sealed record SetDeductionConceptActiveCommand(Guid Id, bool IsActive)
    : IRequest<Result<DeductionConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
