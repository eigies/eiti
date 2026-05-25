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
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IUserRepository _userRepository;

    public GetCurrentCashSessionHandler(
        ICurrentUserService currentUserService,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IPurchaseRepository purchaseRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _purchaseRepository = purchaseRepository;
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

        var ccGroupIds = session.Movements
            .Where(m => m.CcPaymentGroupId.HasValue)
            .Select(m => m.CcPaymentGroupId!.Value)
            .Distinct()
            .ToList();
        IReadOnlyList<SaleCcPayment> ccPayments = ccGroupIds.Count > 0
            ? await _saleRepository.GetCcPaymentsByGroupIdsAsync(ccGroupIds, cancellationToken)
            : [];

        var saleIds = session.Movements
            .Where(m => (m.ReferenceType == CashReferenceTypes.Sale || m.ReferenceType == CashReferenceTypes.CuentaCorriente) && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value)
            .Concat(payments.Select(p => p.SaleId.Value))
            .Distinct()
            .ToList();

        var saleCodes = new Dictionary<Guid, string?>();
        if (saleIds.Count > 0)
        {
            var codes = await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken);
            foreach (var kv in codes) saleCodes[kv.Key] = kv.Value;
        }

        var purchaseIds = session.Movements
            .Where(m => m.ReferenceType == CashReferenceTypes.Purchase && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value)
            .Distinct()
            .ToList();
        if (purchaseIds.Count > 0)
        {
            var purchaseCodes = await _purchaseRepository.GetCodesByPurchaseIdsAsync(purchaseIds, cancellationToken);
            foreach (var kv in purchaseCodes) saleCodes[kv.Key] = kv.Value;
        }

        var userIds = session.Movements
            .Select(m => m.CreatedByUserId.Value)
            .Distinct()
            .ToList();

        var usernames = await _userRepository.GetUsernamesByIdsAsync(userIds, cancellationToken);

        return Result<CashSessionResponse>.Success(CashSessionMapper.Map(session, payments, saleCodes, usernames, ccPayments));
    }
}
