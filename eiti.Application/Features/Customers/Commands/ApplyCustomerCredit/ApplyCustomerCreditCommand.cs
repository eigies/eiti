using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.ApplyCustomerCredit;

// Reconciliación: imputa el saldo a favor existente del cliente a sus ventas CC pendientes (FIFO),
// sin registrar un cobro nuevo ni mover caja. Sirve para sanear datos legacy donde el saldo a favor
// y el saldo pendiente coexisten (el modelo viejo por-venta no imputaba crédito entre ventas).
public sealed record ApplyCustomerCreditCommand(Guid CustomerId)
    : IRequest<Result<ApplyCustomerCreditResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
