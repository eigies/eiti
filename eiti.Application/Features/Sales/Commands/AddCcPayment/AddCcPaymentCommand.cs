using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.AddCcPayment;

public sealed record AddCcPaymentCommand(
    Guid SaleId,
    int IdPaymentMethod,
    decimal Amount,
    DateTime Date,
    string? Notes
) : IRequest<Result<AddCcPaymentResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
