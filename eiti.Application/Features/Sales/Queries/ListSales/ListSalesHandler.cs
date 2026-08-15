using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;


using eiti.Domain.Addresses;
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
    private readonly IAddressRepository _addressRepository;

    public ListSalesHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        IEmployeeRepository employeeRepository,
        IVehicleRepository vehicleRepository,
        IAddressRepository addressRepository)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _employeeRepository = employeeRepository;
        _vehicleRepository = vehicleRepository;
        _addressRepository = addressRepository;
    }

    public async Task<Result<IReadOnlyList<ListSalesItemResponse>>> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticated();
        if (authCheck.IsFailure)
            return Result<IReadOnlyList<ListSalesItemResponse>>.Failure(authCheck.Error);

        // Las fechas llegan como día local del usuario; se traducen al instante UTC equivalente.
        var from = request.DateFrom.HasValue ? BusinessCalendar.StartOfDayUtc(request.DateFrom.Value) : (DateTime?)null;
        var to = request.DateTo.HasValue ? BusinessCalendar.EndOfDayUtc(request.DateTo.Value) : (DateTime?)null;

        var sales = await _saleRepository.ListByCompanyAsync(
            _currentUserService.CompanyId,
            from,
            to,
            request.IdSaleStatus,
            request.IncludeCuentaCorriente,
            cancellationToken);

        if (!_currentUserService.CanViewAllBranches)
        {
            var allowed = _currentUserService.AllowedBranchIds;
            sales = sales.Where(sale => allowed.Contains(sale.BranchId.Value)).ToList();
        }

        var productIds = sales
            .SelectMany(sale => sale.Details.Select(detail => detail.ProductId.Value)
                .Concat(sale.TradeIns.Select(tradeIn => tradeIn.ProductId.Value)))
            .Distinct()
            .ToList();

        var saleIds = sales.Select(sale => sale.Id).ToList();
        var assignments = await _saleTransportAssignmentRepository.ListBySaleIdsAsync(saleIds, _currentUserService.CompanyId, cancellationToken);
        var assignmentMap = assignments.ToDictionary(item => item.SaleId.Value, item => item);

        // Productos en un solo query (antes era N+1: un SELECT por producto).
        var products = productIds.Count == 0
            ? []
            : await _productRepository.GetByIdsAsync(productIds.Select(id => new ProductId(id)), _currentUserService.CompanyId, cancellationToken);
        var productMap = products.ToDictionary(product => product.Id.Value, product => product);

        // Clientes en un solo query (antes era N+1: un SELECT por cliente).
        var customerIds = sales
            .Where(sale => sale.CustomerId is not null)
            .Select(sale => sale.CustomerId!.Value)
            .Distinct()
            .ToList();
        var customers = customerIds.Count == 0
            ? []
            : await _customerRepository.ListByIdsAsync(_currentUserService.CompanyId, customerIds.Select(id => new CustomerId(id)), cancellationToken);
        var customerMap = customers.ToDictionary(customer => customer.Id.Value, customer => customer);

        var addressMap = new Dictionary<Guid, Address>();
        foreach (var customer in customerMap.Values.Where(c => c.AddressId is not null))
        {
            var addressId = customer.AddressId!;
            if (!addressMap.ContainsKey(addressId.Value))
            {
                var address = await _addressRepository.GetByIdAsync(addressId, cancellationToken);
                if (address is not null)
                {
                    addressMap[addressId.Value] = address;
                }
            }
        }

        // Empleados y vehículos: un query por compañía y se arma el mapa (antes era N+1 por asignación).
        var employeeMap = new Dictionary<Guid, Employee>();
        var vehicleMap = new Dictionary<Guid, Vehicle>();
        if (assignments.Count > 0)
        {
            employeeMap = (await _employeeRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken))
                .ToDictionary(employee => employee.Id.Value, employee => employee);
            vehicleMap = (await _vehicleRepository.ListByCompanyAsync(_currentUserService.CompanyId, cancellationToken))
                .ToDictionary(vehicle => vehicle.Id.Value, vehicle => vehicle);
        }

        return Result<IReadOnlyList<ListSalesItemResponse>>.Success(
            sales.Select(sale =>
                {
                    customerMap.TryGetValue(sale.CustomerId?.Value ?? Guid.Empty, out var customer);
                    var customerAddress = customer?.AddressId is not null && addressMap.TryGetValue(customer.AddressId.Value, out var address)
                        ? FormatAddress(address)
                        : null;
                    return new ListSalesItemResponse(
                        sale.Id.Value,
                        sale.Code,
                        sale.BranchId.Value,
                        sale.CustomerId?.Value,
                        customer?.FullName,
                        customer is null ? null : BuildCustomerDocument(customer),
                        customer?.TaxId,
                        customerAddress,
                        string.IsNullOrWhiteSpace(customer?.Phone) ? null : customer.Phone,
                        sale.DeliveryAddress,
                        sale.ContactPhone,
                        sale.CashDrawerId?.Value,
                        sale.CashSessionId?.Value,
                        sale.HasDelivery,
                        sale.TransportAssignmentId?.Value,
                        assignmentMap.TryGetValue(sale.Id.Value, out var assignment) && employeeMap.TryGetValue(assignment.DriverEmployeeId.Value, out var driver) ? driver.FullName : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) && vehicleMap.TryGetValue(assignment.VehicleId.Value, out var vehicle) ? vehicle.Plate : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) ? (int)assignment.Status : null,
                        assignmentMap.TryGetValue(sale.Id.Value, out assignment) ? assignment.Status.ToString() : null,
                        (int)sale.SaleStatus,
                        sale.SaleStatus.ToString(),
                        sale.NoDeliverySurchargeTotal,
                        sale.GeneralDiscountPercent,
                        sale.OriginalTotal,
                        sale.TotalAmount,
                        sale.ManualOverridePrice,
                        sale.MonetaryPaidAmount,
                        sale.TradeInAmount,
                        sale.SettledAmount,
                        sale.PendingAmount,
                        sale.CreatedAt,
                        sale.PaidAt,
                        sale.UpdatedAt,
                        sale.IsModified,
                        sale.IsCuentaCorriente,
                        sale.SourceChannel,
                        sale.Details.Select(detail =>
                        {
                            productMap.TryGetValue(detail.ProductId.Value, out var product);
                            return new ListSalesDetailItemResponse(
                                detail.ProductId.Value,
                                product?.Name ?? "Deleted product",
                                product?.Brand ?? "Unknown",
                                detail.Quantity,
                                detail.UnitPrice,
                                detail.DiscountPercent,
                                detail.TotalAmount);
                        }).ToList(),
                        sale.Payments.Select(payment => new ListSalesPaymentItemResponse(
                            (int)payment.Method,
                            payment.Method.ToString(),
                            payment.Amount,
                            payment.Reference,
                            payment.CardBankId,
                            payment.CardCuotas,
                            payment.CardSurchargeAmt,
                            payment.TransferBankId)).ToList(),
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

    private static string? FormatAddress(Address address)
    {
        if (string.IsNullOrWhiteSpace(address.Street))
            return null;

        var street = address.Street;
        if (!string.IsNullOrWhiteSpace(address.StreetNumber))
            street += $" {address.StreetNumber}";
        if (!string.IsNullOrWhiteSpace(address.Floor))
            street += $", Piso {address.Floor}";
        if (!string.IsNullOrWhiteSpace(address.Apartment))
            street += $", Depto {address.Apartment}";

        return street;
    }
}
