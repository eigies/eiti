using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.ListCashSessionHistory;

public sealed class ListCashSessionHistoryHandler : IRequestHandler<ListCashSessionHistoryQuery, Result<IReadOnlyList<CashSessionResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;

    public ListCashSessionHistoryHandler(ICurrentUserService currentUserService, ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<IReadOnlyList<CashSessionResponse>>> Handle(ListCashSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<CashSessionResponse>>.Failure(Error.Unauthorized("CashSessions.History.Unauthorized", "The current user is not authenticated."));
        }

        var from = request.From?.Date;
        var to = request.To?.Date.AddDays(1).AddTicks(-1);

        if (from.HasValue && to.HasValue && from > to)
        {
            return Result<IReadOnlyList<CashSessionResponse>>.Failure(
                Error.Validation("CashSessions.History.InvalidDateRange", "The selected date range is invalid."));
        }

        var sessions = await _cashSessionRepository.ListByDrawerAsync(
            new CashDrawerId(request.CashDrawerId),
            _currentUserService.CompanyId,
            from,
            to,
            cancellationToken);

        return Result<IReadOnlyList<CashSessionResponse>>.Success(
            sessions.Select(CashSessionMapper.Map).ToList());
    }
}
