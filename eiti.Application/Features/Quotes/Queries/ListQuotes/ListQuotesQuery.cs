using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Quotes;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.ListQuotes;

public sealed record ListQuotesQuery(
    QuoteStatus? Status,
    DateTime? DateFrom,
    DateTime? DateTo,
    Guid? CustomerId
) : IRequest<Result<IReadOnlyList<QuoteListItemResponse>>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesAccess];
}
