using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Common;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

/// <param name="InvoiceLetter">
/// Letra que eligió el usuario al facturar después del alta. Si no coincide con el cliente, no se pide
/// nada al servicio. Null = la decide la condición de IVA del cliente, como antes.
/// </param>
public sealed record InvoiceSaleCommand(Guid Id, InvoiceLetter? InvoiceLetter = null)
    : IRequest<Result<InvoiceSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
