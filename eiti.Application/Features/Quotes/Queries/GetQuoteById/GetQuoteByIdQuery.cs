using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using MediatR;

namespace eiti.Application.Features.Quotes.Queries.GetQuoteById;

public sealed record GetQuoteByIdQuery(Guid QuoteId)
    : IRequest<Result<QuoteDetailResponse>>, IRequirePermissions
{
    public IReadOnlyCollection<string> RequiredPermissions => [PermissionCodes.QuotesAccess];
}
