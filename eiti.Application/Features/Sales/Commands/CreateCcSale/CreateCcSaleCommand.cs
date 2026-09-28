using eiti.Application.Features.Sales.Common;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateSale;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.CreateCcSale;

public sealed record CreateCcSaleCommand(
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details,
    IReadOnlyList<CreateSaleTradeInItemRequest>? TradeIns = null,
    decimal GeneralDiscountPercent = 0,
    decimal? ManualOverridePrice = null,
    decimal? VatRate = null,
    // Igual que en mostrador: opt-in por venta, y se factura igual si la sucursal factura sola.
    bool RequestInvoicing = false,
    // Letra que eligió el vendedor. Con factura pedida, la venta se frena si no coincide con el cliente.
    InvoiceLetter? InvoiceLetter = null
) : IRequest<Result<CreateCcSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.SalesCreate];
}
