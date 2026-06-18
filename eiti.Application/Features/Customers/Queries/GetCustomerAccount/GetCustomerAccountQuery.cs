using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Customers.Common;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.GetCustomerAccount;

public sealed record GetCustomerAccountQuery(Guid CustomerId)
    : IRequest<Result<GetCustomerAccountResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}

public sealed record GetCustomerAccountResponse(
    Guid CustomerId,
    string CustomerName,
    string? Phone,
    string? Email,
    decimal DeudaTotal,
    decimal CobradoTotal,
    decimal SaldoPendiente,
    decimal SaldoAFavor,
    IReadOnlyList<CustomerAccountMovement> Movements);

public sealed record CustomerAccountMovement(
    string Type,            // "venta" | "cobro"
    Guid Id,
    DateTime Date,
    string Description,
    string? Code,           // VENT-XXXX en ventas
    decimal Amount,
    bool IsDebit,
    int Status,
    string StatusName,
    string? Method,
    string? ChequeNumero,
    IReadOnlyList<CustomerPaymentImputacion>? Imputaciones,  // qué ventas cubrió este cobro
    decimal? Sobrante,                                      // excedente del cobro a saldo a favor
    DateTime SortDate);
