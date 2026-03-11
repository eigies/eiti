using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Domain.Customers;
using eiti.Domain.Employees;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Vehicles;
using MediatR;

namespace eiti.Application.Features.Sales.Queries.ListSales;

public sealed class ListSalesHandler : IRequestHandler<ListSalesQuery, Result<IReadOnlyList<ListSalesItemResponse>>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public ListSalesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        IEmployeeRepository employeeRepository,
        IVehicleRepository vehicleRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<IReadOnlyList<ListSalesItemResponse>>> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<IReadOnlyList<ListSalesItemResponse>>.Failure(
                Error.Unauthorized("Sales.List.Unauthorized", "The current user is not authenticated."));
        }

        var sales = await _saleRepository.ListByCompanyAsync(
            _currentUserService.CompanyId,
            request.DateFrom,
            request.DateTo,
            request.IdSaleStatus,
            cancellationToken);

        var productIds = sales
            .SelectMany(sale => sale.Details.Select(detail => detail.ProductId.Value)
                .Concat(sale.TradeIns.Select(tradeIn => tradeIn.ProductId.Value)))
            .Distinct()
            .ToList();

        var productMap = new Dictionary<Guid, Product>();
        var saleIds = sales.Select(sale => sale.Id).ToList();
        var assignments = await _saleTransportAssignmentRepository.ListBySaleIdsAsync(saleIds, _currentUserService.CompanyId, cancellationToken);
        var assignmentMap = assignments.ToDictionary(item => item.SaleId.Value, item => item);
        var employeeMap = new Dictionary<Guid, Employee>();
        var vehicleMap = new Dictionary<Guid, Vehicle>();
        var customerMap = new Dictionary<Guid, Customer>();

        foreach (var productId in productIds)
        {
            var product = await _productRepository.GetByIdAsync(new ProductId(productId), _currentUserService.CompanyId, cancellationToken);
            if (product is not null)
            {
                productMap[productId] = product;
            }
        }

        foreach (var sale in sales.Where(item => item.CustomerId is not null))
        {
            var customerId = sale.CustomerId!.Value;
            if (customerMap.ContainsKey(customerId))
            {
                continue;
            }

            var customer = await _customerRepository.GetByIdAsync(new CustomerId(customerId), _currentUserService.CompanyId, cancellationToken);
            if (customer is not null)
            {
                customerMap[customerId] = customer;
            }
        }

        foreach (var assignment in assignments)
        {
            if (!employeeMap.ContainsKey(assignment.DriverEmployeeId.Value))
            {
                var employee = await _employeeRepository.GetByIdAsync(assignment.DriverEmployeeId, _currentUserService.CompanyId, cancellationToken);
                if (employee is not null)
                {
                    employeeMap[employee.Id.Value] = employee;
                }
            }

            if (!vehicleMap.ContainsKey(assignment.VehicleId.Value))
            {
                var vehicle = await _vehicleRepository.GetByIdAsync(assignment.VehicleId, _currentUserService.CompanyId, cancellationToken);
                if (vehicle is not null)
                {
                    vehicleMap[vehicle.Id.Value] = vehicle;
                }
            }
        }

        return Result<IReadOnlyList<ListSalesItemResponse>>.Success(
            sales.Select(sale =>
                {
                    customerMap.TryGetValue(sale.CustomerId?.Value ?? Guid.Empty, out var customer);
                    return new ListSalesItemResponse(
                        sale.Id.Value,
                        sale.BranchId.Value,
                        sale.CustomerId?.Value,
                        customer?.FullName,
                        customer is null ? null : BuildCustomerDocument(customer),
                        customer?.TaxId,
                        sale.CashSessionId?.Value,
                        sale.HasDelivery,
                        sale.TransportAssignmentId?.Value,
                        assignmentMap.TryGetValue(sale.Id.Value, out var assignment) && employeeMap.TryGetValue(assignment.DriverEmployeeId.Value, out var driver) ? driver.FullName : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) && vehicleMap.TryGetValue(assignment.VehicleId.Value, out var vehicle) ? vehicle.Plate : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) ? (int)assignment.Status : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) ? assignment.Status.ToString() : null,
                        (int)sale.SaleStatus,
                        sale.SaleStatus.ToString(),
                        sale.TotalAmount,
                        sale.MonetaryPaidAmount,
                        sale.TradeInAmount,
                        sale.SettledAmount,
                        sale.PendingAmount,
                        sale.CreatedAt,
                        sale.PaidAt,
                        sale.UpdatedAt,
                        sale.IsModified,
                        sale.Details.Select(detail =>
                        {
                            productMap.TryGetValue(detail.ProductId.Value, out var product);
                            return new ListSalesDetailItemResponse(
                                detail.ProductId.Value,
                                product?.Name ?? "Deleted product",
                                product?.Brand ?? "Unknown",
                                detail.Quantity,
                                detail.UnitPrice,
                                detail.TotalAmount);
                        }).ToList(),
                        sale.Payments.Select(payment => new ListSalesPaymentItemResponse(
                            (int)payment.Method,
                            payment.Method.ToString(),
                            payment.Amount,
                            payment.Reference)).ToList(),
                        sale.TradeIns.Select(tradeIn =>
                        {
                            productMap.TryGetValue(tradeIn.ProductId.Value, out var product);
                            return new ListSalesTradeInItemResponse(
                                tradeIn.ProductId.Value,
                                product?.Name ?? "Deleted product",
                                product?.Brand ?? "Unknown",
                                tradeIn.Quantity,
                                tradeIn.Amount);
                        }).ToList());
                })
                .ToList());
    }

    private static string? BuildCustomerDocument(Customer customer)
    {
        return customer.DocumentType is null || string.IsNullOrWhiteSpace(customer.DocumentNumber)
            ? null
            : $"{customer.DocumentType} {customer.DocumentNumber}";
    }
}
