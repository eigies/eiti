using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Bonuses;
using MediatR;

namespace eiti.Application.Features.Payroll.Bonuses.Commands.CreatePayrollBonus;

public sealed record CreatePayrollBonusCommand(
    Guid EmployeeId,
    Guid ConceptId,
    int AmountType,
    decimal Value,
    string? Notes) : IRequest<Result<PayrollBonusResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollManage];
}
