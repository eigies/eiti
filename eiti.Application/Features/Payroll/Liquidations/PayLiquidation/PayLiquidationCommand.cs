using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Payroll.Liquidations;
using MediatR;

namespace eiti.Application.Features.Payroll.Liquidations.PayLiquidation;

public sealed record PayLiquidationCommand(Guid LiquidationId, int PaymentMethod, Guid? CashSessionId)
    : IRequest<Result<PayrollLiquidationResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PayrollLiquidationsPay];
}
