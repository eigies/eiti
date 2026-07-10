using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.CancelLiquidation;

public sealed record CancelLiquidationCommand(Guid LiquidationId) : IRequest<Result<PayrollLiquidationResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsPay];
}
