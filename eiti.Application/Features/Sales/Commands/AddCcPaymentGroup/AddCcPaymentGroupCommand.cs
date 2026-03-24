using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public sealed record CcPaymentMethodLine(int IdPaymentMethod, decimal Amount);

public sealed record AddCcPaymentGroupCommand(
    Guid SaleId,
    List<CcPaymentMethodLine> Methods,
    DateTime Date,
    string? Notes,
    Guid? CashDrawerId
) : IRequest<Result<AddCcPaymentGroupResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
