using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.DeductionConcepts;
using MediatR;

namespace eiti.Application.Features.Payroll.DeductionConcepts.Commands.CreateDeductionConcept;

public sealed record CreateDeductionConceptCommand(string Name, decimal Percentage)
    : IRequest<Result<DeductionConceptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
