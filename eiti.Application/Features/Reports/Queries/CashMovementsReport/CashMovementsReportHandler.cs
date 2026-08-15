using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Cash;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CashMovementsReport;

public sealed class CashMovementsReportHandler : IRequestHandler<CashMovementsReportQuery, Result<CashMovementsReportResponse>>
{
    private static readonly (CashMovementType Type, string Motivo)[] Categories =
    {
        (CashMovementType.CashDeposit, "Ingreso"),
        (CashMovementType.CashWithdrawal, "Salida"),
        (CashMovementType.CashTransferOut, "Pase"),
        (CashMovementType.PurchaseExpense, "Gasto")
    };

    private readonly ICurrentUserService _currentUserService;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly IUserRepository _userRepository;

    public CashMovementsReportHandler(
        ICurrentUserService currentUserService,
        ICashSessionRepository cashSessionRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _cashSessionRepository = cashSessionRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<CashMovementsReportResponse>> Handle(CashMovementsReportQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CashMovementsReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        // El rango llega como fecha local del usuario; se traduce al instante UTC equivalente.
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var types = Categories.Select(c => c.Type).ToArray();
        var branchIds = _currentUserService.CanViewAllBranches ? null : _currentUserService.AllowedBranchIds;
        var movements = await _cashSessionRepository.ListMovementsByCompanyAsync(companyId, from, to, types, branchIds, cancellationToken);

        var userIds = movements.Select(m => m.CreatedByUserId.Value).Distinct().ToList();
        var usernamesById = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _userRepository.GetUsernamesByIdsAsync(userIds, cancellationToken);

        var byType = movements.GroupBy(m => m.Type).ToDictionary(g => g.Key, g => g.ToList());

        var categories = new List<CashMovementCategory>();
        foreach (var (type, motivo) in Categories)
        {
            byType.TryGetValue(type, out var list);
            list ??= new List<CashMovement>();

            var items = list
                .OrderByDescending(m => m.OccurredAt)
                .Select(m => new CashMovementItem(
                    m.OccurredAt,
                    m.Description,
                    m.Amount,
                    usernamesById.TryGetValue(m.CreatedByUserId.Value, out var name) ? name : null))
                .ToList();

            categories.Add(new CashMovementCategory(
                motivo,
                (int)type,
                list.Sum(m => m.Amount),
                list.Count,
                items));
        }

        return Result<CashMovementsReportResponse>.Success(new CashMovementsReportResponse(
            categories,
            categories.Sum(c => c.Total)));
    }
}
