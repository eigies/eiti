using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.AddSupplierPayment;

public sealed record AddSupplierPaymentCommand(
    Guid SupplierId,
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    Guid? ChequeId = null
) : IRequest<Result<AddSupplierPaymentResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesPay];
}
