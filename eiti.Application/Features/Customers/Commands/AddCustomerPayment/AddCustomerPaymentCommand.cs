using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.AddCustomerPayment;

// Cheque recibido del cliente que ENTRA a cartera (se crea referenciando el CustomerPayment).
public sealed record AddCustomerPaymentChequeData(
    int BankId,
    string Numero,
    string Titular,
    string CuitDni,
    decimal Monto,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string? Notas);

public sealed record AddCustomerPaymentCommand(
    Guid CustomerId,
    int Method,
    decimal Amount,
    DateTime Date,
    string? Reference,
    string? Notes,
    int? CardBankId = null,
    int? CardCuotas = null,
    AddCustomerPaymentChequeData? Cheque = null
) : IRequest<Result<AddCustomerPaymentResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesPay];
}
