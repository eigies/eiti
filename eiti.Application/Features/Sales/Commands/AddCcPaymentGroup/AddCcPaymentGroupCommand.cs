using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.AddCcPaymentGroup;

public sealed record ChequeData(
    string Numero,
    int BankId,
    string Titular,
    string CuitDni,
    decimal Monto,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    string? Notas);

public sealed record CcPaymentMethodLine(
    int IdPaymentMethod,
    decimal Amount,
    int? CardBankId = null,
    int? CardCuotas = null,
    ChequeData? Cheque = null);

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
