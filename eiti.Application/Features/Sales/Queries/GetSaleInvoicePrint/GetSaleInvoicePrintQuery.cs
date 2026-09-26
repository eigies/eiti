using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePrint;

/// <summary>
/// Datos para que el front arme el PDF del comprobante con el diseño de EITI (logo, marca de agua,
/// ítems). <see cref="Kind"/> elige entre la factura de la venta y la nota de crédito que la anuló.
/// </summary>
public sealed record GetSaleInvoicePrintQuery(Guid SaleId, SaleFiscalDocumentKind Kind)
    : IRequest<Result<SaleInvoicePrintResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}
