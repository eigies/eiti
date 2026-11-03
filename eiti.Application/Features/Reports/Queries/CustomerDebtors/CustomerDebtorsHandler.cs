using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using MediatR;

namespace eiti.Application.Features.Reports.Queries.CustomerDebtors;

public sealed class CustomerDebtorsHandler : IRequestHandler<CustomerDebtorsQuery, Result<CustomerDebtorsResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;

    public CustomerDebtorsHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<CustomerDebtorsResponse>> Handle(CustomerDebtorsQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<CustomerDebtorsResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        CustomerId? customerId = request.CustomerId is { } id ? new CustomerId(id) : null;

        var sales = (await _saleRepository.ListCcSalesByCompanyAsync(companyId, customerId, cancellationToken))
            .Where(s => s.SaleStatus != SaleStatus.Cancel && s.CustomerId is not null && s.CcPendingAmount > 0)
            .Where(s => request.BranchId is null || s.BranchId.Value == request.BranchId)
            .ToList();

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            sales = sales.Where(s => allowed.Contains(s.BranchId.Value)).ToList();
        }

        var grouped = sales
            .GroupBy(s => s.CustomerId!.Value)
            .ToList();

        var rows = new List<CustomerDebtorRow>();
        foreach (var group in grouped)
        {
            var debtorId = group.Key;
            var customer = await _customerRepository.GetByIdAsync(new CustomerId(debtorId), companyId, cancellationToken);

            var owed = decimal.Round(group.Sum(s => s.CcPendingAmount), 2, MidpointRounding.AwayFromZero);
            // El saldo a favor es del cliente, no de una sucursal: filtrar por sucursal no lo reparte.
            var credit = customer?.CreditBalance ?? 0m;

            rows.Add(new CustomerDebtorRow(
                debtorId,
                customer?.FullName ?? "(Cliente)",
                group.Count(),
                group.Min(s => s.CreatedAt),
                owed,
                credit,
                decimal.Round(owed - credit, 2, MidpointRounding.AwayFromZero)));
        }

        rows = rows.OrderByDescending(r => r.Owed).ToList();

        return Result<CustomerDebtorsResponse>.Success(new CustomerDebtorsResponse(
            rows,
            rows.Sum(r => r.Owed),
            rows.Sum(r => r.CreditBalance),
            rows.Sum(r => r.Net)));
    }
}
