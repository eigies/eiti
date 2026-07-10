using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Advances;
using MediatR;

namespace eiti.Application.Features.Payroll.Advances.Commands.CreatePayrollAdvance;

public sealed record CreatePayrollAdvanceCommand(
    Guid EmployeeId,
    decimal Amount,
    DateTime Date,
    string? Notes,
    int PaymentMethod,
    Guid? CashSessionId) : IRequest<Result<PayrollAdvanceResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollAdvancesManage];
}
