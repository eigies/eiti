using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.GetCurrentCashSession;

public sealed class GetCurrentCashSessionHandler : IRequestHandler<GetCurrentCashSessionQuery, Result<CashSessionResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUserRepository _userRepository;

    public GetCurrentCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<CashSessionResponse>> Handle(GetCurrentCashSessionQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(authCheck.Error);

        var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            new CashDrawerId(request.CashDrawerId),
            cancellationToken);
        if (accessCheck.IsFailure)
            return Result<CashSessionResponse>.Failure(accessCheck.Error!);

        var session = await _cashSessionRepository.GetOpenByDrawerAsync(new CashDrawerId(request.CashDrawerId), _currentUserService.CompanyId, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionResponse>.Failure(Error.NotFound("CashSessions.Current.NotFound", "There is no open cash session for the requested cash drawer."));
        }

        IReadOnlyList<SalePayment> payments = await _saleRepository.GetPaymentsByCashSessionIdAsync(session.Id, cancellationToken);

        var saleIds = payments.Select(p => p.SaleId.Value).Distinct().ToList();

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
