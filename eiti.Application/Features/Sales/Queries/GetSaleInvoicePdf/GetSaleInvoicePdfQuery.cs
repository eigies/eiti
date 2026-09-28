using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.GetSaleInvoicePdf;

public sealed record GetSaleInvoicePdfQuery(Guid SaleId)
    : IRequest<Result<SaleInvoicePdfResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesAccess];
}

public sealed record SaleInvoicePdfResponse(byte[] Content, string FileName);
