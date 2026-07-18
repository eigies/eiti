using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Quotes.Commands.CreateQuote;

public sealed record CreateQuoteDetailItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountPercent = 0);

public sealed record CreateQuoteCommand(
    Guid BranchId,
    Guid? CustomerId,
    string? ProspectName,
    string? ProspectContact,
    IReadOnlyList<CreateQuoteDetailItemRequest> Details,
    decimal GeneralDiscountPercent,
    DateTime ExpiresAt
) : IRequest<Result<CreateQuoteResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesCreate];
}
