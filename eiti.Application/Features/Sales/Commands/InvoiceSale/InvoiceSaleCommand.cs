using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.InvoiceSale;

public sealed record InvoiceSaleCommand(Guid Id) : IRequest<Result<InvoiceSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesInvoice];
}
