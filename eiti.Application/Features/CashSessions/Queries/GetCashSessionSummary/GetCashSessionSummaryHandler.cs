using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCashSessionSummary;

public sealed class GetCashSessionSummaryHandler : IRequestHandler<GetCashSessionSummaryQuery, Result<CashSessionSummaryResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;

    public GetCashSessionSummaryHandler(ICurrentUserService currentUserService, ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<CashSessionSummaryResponse>> Handle(GetCashSessionSummaryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashSessionSummaryResponse>.Failure(authCheck.Error);

        var session = await _cashSessionRepository.GetByIdAsync(new CashSessionId(request.Id), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionSummaryResponse>.Failure(Error.NotFound("CashSessions.Summary.NotFound", "The requested cash session was not found."));
        }

        return Result<CashSessionSummaryResponse>.Success(CashSessionMapper.MapSummary(session));
    }
}
