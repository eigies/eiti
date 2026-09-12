using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.CashSessions.Common;
using eiti.Domain.Cash;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CashSessionsReport;

public sealed class CashSessionsReportHandler
    : IRequestHandler<CashSessionsReportQuery, Result<CashSessionsReportResponse>>
{
    // Movimientos que son espejo de una venta (o el fondo de apertura): quedan fuera de otherMovements.
    private static readonly HashSet<CashMovementType> MirrorTypes =
    [
        CashMovementType.OpeningFloat,
        CashMovementType.SaleIncome,
        CashMovementType.TransferIncome,
        CashMovementType.CardIncome
    ];

    private const int MaxOtherMovementsPerSession = 50;

    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchRepository _branchRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IUserRepository _userRepository;

    public CashSessionsReportHandler(
        ICurrentUserService currentUserService,
        IBranchRepository branchRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleRepository saleRepository,
        IPurchaseRepository purchaseRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _branchRepository = branchRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleRepository = saleRepository;
        _purchaseRepository = purchaseRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<CashSessionsReportResponse>> Handle(
        CashSessionsReportQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashSessionsReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var dateFromLabel = request.DateFrom.ToString("yyyy-MM-dd");
        var dateToLabel = request.DateTo.ToString("yyyy-MM-dd");

        // Sucursales visibles: null = todas. Si se pide una branchId no permitida, la respuesta va vacía.
        HashSet<Guid>? branchScope = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds.ToHashSet();

        if (request.BranchId.HasValue)
        {
            if (branchScope is not null && !branchScope.Contains(request.BranchId.Value))
                return Result<CashSessionsReportResponse>.Success(
                    new CashSessionsReportResponse(dateFromLabel, dateToLabel, []));

            branchScope = [request.BranchId.Value];
        }

        // El rango llega como fecha local del usuario; se traduce al instante UTC equivalente.
        var (fromUtc, toUtc) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var branchFilter = branchScope?.ToList();

        var branches = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
            .Where(branch => branchScope is null || branchScope.Contains(branch.Id.Value))
            .OrderBy(branch => branch.Name)
            .ToList();

        if (branches.Count == 0)
            return Result<CashSessionsReportResponse>.Success(
                new CashSessionsReportResponse(dateFromLabel, dateToLabel, []));

        var drawers = (await _cashDrawerRepository.ListByCompanyAsync(companyId, cancellationToken))
            .Where(drawer => drawer.IsActive
                && (branchScope is null || branchScope.Contains(drawer.BranchId.Value)))
            .ToList();

        var sessions = await _cashSessionRepository.ListByCompanyAsync(
            companyId, fromUtc, toUtc, branchFilter, cancellationToken);

        var (paymentsBySaleId, saleCodes, usernames) =
            await LoadLookupsAsync(sessions, cancellationToken);

        var drawersByBranch = drawers
            .GroupBy(drawer => drawer.BranchId.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(d => d.Name).ToList());

        var sessionsByDrawer = sessions
            .GroupBy(session => session.CashDrawerId.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(s => s.OpenedAt).ToList());

        var branchNodes = branches.Select(branch =>
        {
            drawersByBranch.TryGetValue(branch.Id.Value, out var branchDrawers);
            branchDrawers ??= [];

            var drawerNodes = branchDrawers.Select(drawer =>
            {
                sessionsByDrawer.TryGetValue(drawer.Id.Value, out var drawerSessions);
                drawerSessions ??= [];

                var sessionNodes = drawerSessions
                    .Select(session => MapSession(session, paymentsBySaleId, saleCodes, usernames))
                    .ToList();

                return new CashSessionsReportDrawer(drawer.Id.Value, drawer.Name, sessionNodes);
            }).ToList();

            return new CashSessionsReportBranch(branch.Id.Value, branch.Name, drawerNodes);
        }).ToList();

        return Result<CashSessionsReportResponse>.Success(
            new CashSessionsReportResponse(dateFromLabel, dateToLabel, branchNodes));
    }

    private CashSessionsReportSession MapSession(
        CashSession session,
        IReadOnlyDictionary<Guid, List<SalePayment>> paymentsBySaleId,
        IReadOnlyDictionary<Guid, string?> saleCodes,
        IReadOnlyDictionary<Guid, string> usernames)
    {
        var totalsByType = session.Movements
            .GroupBy(movement => movement.Type)
            .OrderBy(group => (int)group.Key)
            .Select(group => new CashSessionsReportTypeTotal(
                (int)group.Key,
                group.Key.ToString(),
                group.Count(),
                group.Sum(m => m.Amount)))
            .ToList();

        var sessionSaleIds = session.Movements
            .Where(movement => (movement.Type is CashMovementType.SaleIncome
                    or CashMovementType.TransferIncome
                    or CashMovementType.CardIncome)
                && movement.ReferenceId.HasValue)
            .Select(movement => movement.ReferenceId!.Value)
            .Distinct();

        var sessionPayments = sessionSaleIds
            .SelectMany(saleId => paymentsBySaleId.TryGetValue(saleId, out var list) ? list : [])
            .ToList();

        var paymentBreakdown = CashSessionMapper.BuildPaymentBreakdown(session.Movements, sessionPayments);

        var otherMovements = session.Movements
            .Where(movement => !MirrorTypes.Contains(movement.Type))
            .OrderByDescending(movement => movement.OccurredAt)
            .Take(MaxOtherMovementsPerSession)
            .Select(movement => new CashSessionsReportOtherMovement(
                (int)movement.Type,
                movement.Type.ToString(),
                movement.Amount,
                movement.OccurredAt,
                movement.Description,
                movement.ReferenceId.HasValue ? saleCodes.GetValueOrDefault(movement.ReferenceId.Value) : null,
                usernames.GetValueOrDefault(movement.CreatedByUserId.Value)))
            .ToList();

        return new CashSessionsReportSession(
            session.Id.Value,
            (int)session.Status,
            session.Status.ToString(),
            session.OpenedAt,
            session.ClosedAt,
            session.OpeningAmount,
            session.ExpectedClosingAmount,
            session.ActualClosingAmount,
            session.Difference,
            session.Notes,
            totalsByType,
            paymentBreakdown,
            otherMovements);
    }

    private async Task<(
        Dictionary<Guid, List<SalePayment>> PaymentsBySaleId,
        Dictionary<Guid, string?> SaleCodes,
        Dictionary<Guid, string> Usernames)> LoadLookupsAsync(
        IReadOnlyList<CashSession> sessions,
        CancellationToken cancellationToken)
    {
        var sessionIds = sessions.Select(session => session.Id).ToList();

        var payments = sessionIds.Count > 0
            ? await _saleRepository.GetPaymentsByCashSessionIdsAsync(sessionIds, cancellationToken)
            : [];

        var paymentsBySaleId = payments
            .GroupBy(payment => payment.SaleId.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var movements = sessions.SelectMany(session => session.Movements).ToList();

        var saleIds = movements
            .Where(movement => (movement.ReferenceType == CashReferenceTypes.Sale
                    || movement.ReferenceType == CashReferenceTypes.CuentaCorriente)
                && movement.ReferenceId.HasValue)
            .Select(movement => movement.ReferenceId!.Value)
            .Concat(payments.Select(payment => payment.SaleId.Value))
            .Distinct()
            .ToList();

        var saleCodes = new Dictionary<Guid, string?>();
        if (saleIds.Count > 0)
        {
            foreach (var kv in await _saleRepository.GetCodesBySaleIdsAsync(saleIds, cancellationToken))
                saleCodes[kv.Key] = kv.Value;
        }

        var purchaseIds = movements
            .Where(movement => movement.ReferenceType == CashReferenceTypes.Purchase && movement.ReferenceId.HasValue)
            .Select(movement => movement.ReferenceId!.Value)
            .Distinct()
            .ToList();
        if (purchaseIds.Count > 0)
        {
            foreach (var kv in await _purchaseRepository.GetCodesByPurchaseIdsAsync(purchaseIds, cancellationToken))
                saleCodes[kv.Key] = kv.Value;
        }

        var userIds = movements.Select(movement => movement.CreatedByUserId.Value).Distinct().ToList();
        var usernames = userIds.Count > 0
            ? await _userRepository.GetUsernamesByIdsAsync(userIds, cancellationToken)
            : new Dictionary<Guid, string>();

        return (paymentsBySaleId, saleCodes, usernames);
    }
}
