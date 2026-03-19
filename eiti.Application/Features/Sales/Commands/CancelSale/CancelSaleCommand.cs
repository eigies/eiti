using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CancelSale;

public sealed record CancelSaleCommand(Guid Id) : IRequest<Result>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesDelete];
}
