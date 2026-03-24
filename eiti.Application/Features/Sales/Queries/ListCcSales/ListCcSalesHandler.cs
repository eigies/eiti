using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListCcSales;

public sealed class ListCcSalesHandler : IRequestHandler<ListCcSalesQuery, Result<IReadOnlyList<ListCcSalesItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;

    public ListCcSalesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Result<IReadOnlyList<ListCcSalesItemResponse>>> Handle(ListCcSalesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ListCcSalesItemResponse>>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId;
        if (companyId is null)
        {
            return Result<IReadOnlyList<ListCcSalesItemResponse>>.Failure(
                Error.Unauthorized("Sales.ListCcSales.Unauthorized", "The current user is not authenticated."));
        }

        CustomerId? customerId = request.CustomerId.HasValue
            ? new CustomerId(request.CustomerId.Value)
            : null;

        var sales = await _saleRepository.ListCcSalesByCompanyAsync(companyId, customerId, cancellationToken);

        var customerMap = new Dictionary<Guid, Customer>();
        foreach (var sale in sales.Where(s => s.CustomerId is not null))
        {
            var cid = sale.CustomerId!.Value;
            if (customerMap.ContainsKey(cid))
                continue;

            var customer = await _customerRepository.GetByIdAsync(new CustomerId(cid), companyId, cancellationToken);
            if (customer is not null)
            {
                customerMap[cid] = customer;
            }
        }

        var result = sales.Select(sale =>
        {
            customerMap.TryGetValue(sale.CustomerId?.Value ?? Guid.Empty, out var customer);
            return new ListCcSalesItemResponse(
                sale.Id.Value,
                sale.Code,
                customer?.FullName,
                sale.CreatedAt,
                sale.TotalAmount,
                sale.CcPaidTotal,
                sale.CcPendingAmount,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.IsCuentaCorriente);
        }).ToList();

        return Result<IReadOnlyList<ListCcSalesItemResponse>>.Success(result);
    }
}
