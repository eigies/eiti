using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Customers.Queries.ListCustomerAccounts;

public sealed class ListCustomerAccountsHandler
    : IRequestHandler<ListCustomerAccountsQuery, Result<ListCustomerAccountsResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleRepository _saleRepository;

    public ListCustomerAccountsHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ISaleRepository saleRepository)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _saleRepository = saleRepository;
    }

    public async Task<Result<ListCustomerAccountsResponse>> Handle(
        ListCustomerAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<ListCustomerAccountsResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;

        var pendingByCustomer = await _saleRepository.GetPendingCcTotalsByCustomerAsync(companyId, cancellationToken);
        var ccSales = await _saleRepository.ListCcSalesByCompanyAsync(companyId, null, cancellationToken);
        var customersWithCredit = await _customerRepository.ListWithPositiveCreditAsync(companyId, cancellationToken);

        // Universo de clientes con cuenta corriente: historial CC, pendiente o saldo a favor.
        var customerIds = ccSales
            .Where(sale => sale.CustomerId is not null)
            .Select(sale => sale.CustomerId!.Value)
            .Concat(pendingByCustomer.Keys)
            .Concat(customersWithCredit.Select(c => c.Id.Value))
            .Distinct()
            .Select(id => new CustomerId(id))
            .ToList();

        var customers = await _customerRepository.ListByIdsAsync(companyId, customerIds, cancellationToken);
        var customerById = customers.ToDictionary(c => c.Id.Value);

        var items = customerIds
            .Select(id =>
            {
                var saldoPendiente = pendingByCustomer.TryGetValue(id.Value, out var pending) ? pending : 0m;
                customerById.TryGetValue(id.Value, out var customer);
                var saldoAFavor = customer?.CreditBalance ?? 0m;

                return new CustomerAccountListItem(
                    id.Value,
                    customer?.Name ?? string.Empty,
                    customer?.Phone,
                    customer?.DocumentNumber,
                    customer?.TaxId,
                    saldoPendiente,
                    saldoAFavor);
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalized = request.Search.Trim().ToLowerInvariant();
            var normalizedDigits = DigitsOnly(normalized);
            items = items
                .Where(item => item.Name.ToLowerInvariant().Contains(normalized)
                    || Contains(item.Phone, normalized, normalizedDigits)
                    || Contains(item.DocumentNumber, normalized, normalizedDigits)
                    || Contains(item.TaxId, normalized, normalizedDigits))
                .ToList();
        }

        var ordered = items
            .OrderBy(item => item.Name)
            .ToList();

        return Result<ListCustomerAccountsResponse>.Success(new ListCustomerAccountsResponse(ordered));
    }

    private static bool Contains(string? value, string normalizedSearch, string normalizedSearchDigits)
    {
        if (value is null)
        {
            return false;
        }

        if (value.ToLowerInvariant().Contains(normalizedSearch))
        {
            return true;
        }

        var valueDigits = DigitsOnly(value);
        return normalizedSearchDigits.Length > 0 && valueDigits.Contains(normalizedSearchDigits);
    }

    private static string DigitsOnly(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }
}
