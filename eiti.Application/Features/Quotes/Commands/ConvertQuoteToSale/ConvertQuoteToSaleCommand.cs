using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.Sales.Commands.CreateCcSale;
using eiti.Application.Features.Sales.Commands.CreateSale;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.ConvertQuoteToSale;

public sealed record ConvertQuoteToSaleCommand(
    Guid QuoteId,
    Guid BranchId,
    Guid CustomerId,
    IReadOnlyList<CreateSaleDetailItemRequest> Details,
    IReadOnlyList<CreateSaleTradeInItemRequest>? TradeIns = null,
    decimal GeneralDiscountPercent = 0,
    decimal? ManualOverridePrice = null
) : IRequest<Result<CreateCcSaleResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesConvert];
}
