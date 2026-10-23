using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Customers.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.AddCustomerReceipt;

// Un medio del recibo. Tarjeta: banco + cuotas para el recargo. Transferencia: banco que la recibió.
// Cheque recibido: sus datos (entra a cartera).
public sealed record AddCustomerReceiptLine(
    int Method,
    decimal Amount,
    string? Reference,
    int? CardBankId = null,
    int? CardCuotas = null,
    CustomerPaymentChequeInput? Cheque = null,
    int? TransferBankId = null);

public sealed record AddCustomerReceiptCommand(
    Guid CustomerId,
    DateTime Date,
    string? Notes,
    IReadOnlyList<AddCustomerReceiptLine> Lines
) : IRequest<Result<AddCustomerReceiptResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
