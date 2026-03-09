using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;

public sealed class GetCurrentCashSessionHandler : IRequestHandler<GetCurrentCashSessionQuery, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;

    public GetCurrentCashSessionHandler(ICurrentUserService currentUserService, ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<CashSessionResponse>> Handle(GetCurrentCashSessionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<CashSessionResponse>.Failure(Error.Unauthorized("CashSessions.Current.Unauthorized", "The current user is not authenticated."));
        }

        var session = await _cashSessionRepository.GetOpenByDrawerAsync(new CashDrawerId(request.CashDrawerId), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Current.NotFound", "There is no open cash session for the requested cash drawer."));
        }

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session));
    }
}
