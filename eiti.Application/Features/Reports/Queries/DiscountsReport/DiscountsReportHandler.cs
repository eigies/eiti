using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.DiscountsReport;

public sealed class DiscountsReportHandler
    : IRequestHandler<DiscountsReportQuery, Result<DiscountsReportResponse>>
{
    private const string FinalConsumer = "Consumidor final";
    private const string DeletedCustomer = "Cliente eliminado";
    private const string DeletedUser = "Usuario eliminado";

    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUserRepository _userRepository;

    public DiscountsReportHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IBranchRepository branchRepository,
        ICustomerRepository customerRepository,
        IUserRepository userRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _branchRepository = branchRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
    }

    public async Task<Result<DiscountsReportResponse>> Handle(
        DiscountsReportQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<DiscountsReportResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        // El rango llega como fecha local del usuario; se traduce al instante UTC equivalente.
        var (from, to) = BusinessCalendar.ToUtcRange(request.DateFrom, request.DateTo);

        var allowedBranchIds = _currentUserService.CanViewAllBranches
            ? null
            : _currentUserService.AllowedBranchIds;

        // El repositorio ya empuja la regla a SQL; se repite acá porque es la definición del reporte.
        var sales = (await _saleRepository.ListForDiscountReportAsync(
                companyId, from, to, request.BranchId, request.UserId, allowedBranchIds, cancellationToken))
            .Where(sale => sale.SaleStatus != SaleStatus.Cancel && sale.GeneralDiscountPercent > 0)
            .ToList();

        var branchNames = (await _branchRepository.ListByCompanyAsync(companyId, cancellationToken))
            .ToDictionary(branch => branch.Id.Value, branch => branch.Name);

        var customerIds = sales
            .Where(sale => sale.CustomerId is not null)
            .Select(sale => sale.CustomerId!)
            .Distinct()
            .ToList();
        var customerNames = customerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _customerRepository.ListByIdsAsync(companyId, customerIds, cancellationToken))
                .ToDictionary(customer => customer.Id.Value, customer => customer.FullName);

        var userIds = sales
            .Where(sale => sale.GeneralDiscountByUserId.HasValue)
            .Select(sale => sale.GeneralDiscountByUserId!.Value)
            .Distinct()
            .ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _userRepository.GetUsernamesByIdsAsync(userIds, cancellationToken);

        var rows = sales
            .OrderByDescending(sale => sale.CreatedAt)
            .Select(sale => new DiscountsReportRow(
                sale.Id.Value,
                sale.Code,
                sale.CreatedAt,
                branchNames.TryGetValue(sale.BranchId.Value, out var branchName) ? branchName : "(Sucursal)",
                CustomerName(sale.CustomerId, customerNames),
                sale.IsCuentaCorriente,
                sale.GeneralDiscountByUserId,
                UserName(sale.GeneralDiscountByUserId, userNames),
                sale.GeneralDiscountPercent,
                sale.OriginalTotal,
                DiscountAmount(sale),
                sale.TotalAmount,
                sale.GeneralDiscountReason))
            .ToList();

        var totals = new DiscountsReportTotals(rows.Count, Round(rows.Sum(row => row.DiscountAmount)));

        return Result<DiscountsReportResponse>.Success(new DiscountsReportResponse(rows, totals));
    }

    // Misma cuenta que Sale.RecalculateTotal, sin el precio manual: lo que restó el porcentaje.
    private static decimal DiscountAmount(Sale sale) =>
        Round(sale.OriginalTotal - Round(sale.OriginalTotal * (1m - sale.GeneralDiscountPercent / 100m)));

    private static string CustomerName(CustomerId? customerId, IReadOnlyDictionary<Guid, string> names) =>
        customerId is null
            ? FinalConsumer
            : names.TryGetValue(customerId.Value, out var name) ? name : DeletedCustomer;

    private static string? UserName(Guid? userId, IReadOnlyDictionary<Guid, string> names) =>
        userId is null
            ? null
            : names.TryGetValue(userId.Value, out var name) ? name : DeletedUser;

    private static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
