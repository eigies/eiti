using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.GeneratePayrollPeriod;

public sealed record GeneratePayrollPeriodCommand(int Periodicity, string PeriodLabel, DateTime PeriodStart, DateTime PeriodEnd)
    : IRequest<Result<GeneratePayrollPeriodResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsGenerate];
}
