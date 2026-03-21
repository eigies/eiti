using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetLastClosedCashSession;

public sealed class GetLastClosedCashSessionHandler : IRequestHandler<GetLastClosedCashSessionQuery, Result<LastClosedCashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;

    public GetLastClosedCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
    }

    public async Task<Result<LastClosedCashSessionResponse>> Handle(GetLastClosedCashSessionQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<LastClosedCashSessionResponse>.Failure(authCheck.Error);

        var session = await _cashSessionRepository.GetLastClosedByDrawerAsync(
            new CashDrawerId(request.CashDrawerId),
            _currentUserService.CompanyId,
            cancellationToken);

        var suggestedAmount = session?.ActualClosingAmount ?? 0m;

        return Result<LastClosedCashSessionResponse>.Success(new LastClosedCashSessionResponse(suggestedAmount));
    }
}
