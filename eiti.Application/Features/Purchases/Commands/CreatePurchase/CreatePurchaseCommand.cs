using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Purchases.Commands.CreatePurchase;

public sealed record CreatePurchaseCommand(
    Guid BranchId,
    Guid SupplierId,
    string? InvoiceNumber,
    string? Notes,
    decimal? IvaPct,
    decimal? IngresosBrutosPct,
    IReadOnlyList<CreatePurchaseDetailRequest> Details
) : IRequest<Result<CreatePurchaseResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.PurchasesCreate];
}

public sealed record CreatePurchaseDetailRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost);
