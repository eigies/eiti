using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;

public sealed class GetCurrentCashSessionHandler : IRequestHandler<GetCurrentCashSessionQuery, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUserRepository _userRepository;

    public GetCurrentCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<CashSessionResponse>> Handle(GetCurrentCashSessionQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(authCheck.Error);

        var session = await _cashSessionRepository.GetOpenByDrawerAsync(new CashDrawerId(request.CashDrawerId), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Current.NotFound", "There is no open cash session for the requested cash drawer."));
        }

        var saleIds = session.Movements
            .Where(movement => movement.ReferenceId.HasValue)
            .Select(movement => movement.ReferenceId!.Value)
            .Distinct()
            .ToList();

        IReadOnlyList<SalePayment> payments = saleIds.Count > 0
            ? await _saleRepository.GetPaymentsBySaleIdsAsync(saleIds, cancellationToken)
            : [];

        Dictionary<Guid, string?> saleCodes = saleIds.Count > 0
            ? await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken)
            : [];

        var userIds = session.Movements
            .Select(m => m.CreatedByUserId.Value)
            .Distinct()
            .ToList();

        var usernames = await _userRepository.GetUsernamesByIdsAsync(userIds, cancellationToken);

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session, payments, saleCodes, usernames));
    }
}
