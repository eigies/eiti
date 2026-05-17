using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.CashSessions.Queries.ListCashSessionHistory;

public sealed class ListCashSessionHistoryHandler : IRequestHandler<ListCashSessionHistoryQuery, Result<IReadOnlyList<CashSessionResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IUserRepository _userRepository;

    public ListCashSessionHistoryHandler(
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

    public async Task<Result<IReadOnlyList<CashSessionResponse>>> Handle(ListCashSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<CashSessionResponse>>.Failure(authCheck.Error);

        var accessCheck = await CashDrawerAccessPolicy.EnsureCanAccessDrawerAsync(
            _currentUserService,
            _cashDrawerRepository,
            new CashDrawerId(request.CashDrawerId),
            cancellationToken);
        if (accessCheck.IsFailure)
            return Result<IReadOnlyList<CashSessionResponse>>.Failure(accessCheck.Error!);

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

        // Load all payments for these sessions (includes transfer-only sales with no cash movements)
        var sessionIds = sessions.Select(s => s.Id).ToList();
        IReadOnlyList<SalePayment> allPayments = await _saleRepository.GetPaymentsByCashSessionIdsAsync(sessionIds, cancellationToken);

        var allSaleIds = sessions
            .SelectMany(s => s.Movements)
            .Where(m => m.ReferenceType == CashReferenceTypes.Sale && m.ReferenceId.HasValue)
            .Select(m => m.ReferenceId!.Value)
            .Concat(allPayments.Select(p => p.SaleId.Value))
            .Distinct()
            .ToList();

        Dictionary<Guid, string?> allSaleCodes = allSaleIds.Count > 0
            ? await _saleRepository.GetCodesBySaleIdsAsync(allSaleIds, cancellationToken)
            : [];

        var allUserIds = sessions
            .SelectMany(s => s.Movements)
            .Select(m => m.CreatedByUserId.Value)
            .Distinct()
            .ToList();

        var allUsernames = await _userRepository.GetUsernamesByIdsAsync(allUserIds, cancellationToken);

        // Group payments by SaleId for efficient per-session lookup
        var paymentsBySaleId = allPayments
            .GroupBy(payment => payment.SaleId.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = sessions.Select(session =>
        {
            var sessionSaleIds = session.Movements
                .Where(movement => (movement.Type == CashMovementType.SaleIncome || movement.Type == CashMovementType.TransferIncome || movement.Type == CashMovementType.CardIncome) && movement.ReferenceId.HasValue)
                .Select(movement => movement.ReferenceId!.Value)
                .Distinct();

            var sessionPayments = sessionSaleIds
                .SelectMany(saleId => paymentsBySaleId.TryGetValue(saleId, out var payments) ? payments : [])
                .ToList();

            return CashSessionMapper.Map(session, sessionPayments, allSaleCodes, allUsernames);
        }).ToList();

        return Result<IReadOnlyList<CashSessionResponse>>.Success(result);
    }
}
