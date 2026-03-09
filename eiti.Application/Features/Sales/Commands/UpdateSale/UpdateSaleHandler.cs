using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Common.Authorization;
using eiti.Domain.Cash;
using eiti.Domain.Customers;
using eiti.Domain.Products;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using eiti.Domain.Transport;
using MediatR;

namespace eiti.Application.Features.Sales.Commands.UpdateSale;

public sealed class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, Result<UpdateSaleResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISaleRepository _saleRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISaleTransportAssignmentRepository _saleTransportAssignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSaleHandler(
        ICurrentUserService currentUserService,
        ISaleRepository saleRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        ICashSessionRepository cashSessionRepository,
        ISaleTransportAssignmentRepository saleTransportAssignmentRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _saleRepository = saleRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _cashSessionRepository = cashSessionRepository;
        _saleTransportAssignmentRepository = saleTransportAssignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UpdateSaleResponse>> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.CompanyId is null)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Unauthorized("Sales.Update.Unauthorized", "The current user is not authenticated."));
        }

        if (!Enum.IsDefined(typeof(SaleStatus), request.IdSaleStatus))
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Validation("Sales.Update.InvalidStatus", "The selected sale status is invalid."));
        }

        var sale = await _saleRepository.GetByIdAsync(new SaleId(request.Id), cancellationToken);
        if (sale is null || sale.CompanyId != _currentUserService.CompanyId)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.NotFound("Sales.Update.NotFound", "The requested sale was not found."));
        }

        if (sale.SaleStatus != SaleStatus.OnHold)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Conflict("Sales.Update.NotEditable", "Only sales in OnHold status can be modified."));
        }

        var existingTransportAssignmentId = sale.TransportAssignmentId;

        Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(new CustomerId(request.CustomerId.Value), _currentUserService.CompanyId, cancellationToken);
            if (customer is null)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.NotFound("Sales.Update.CustomerNotFound", "The selected customer was not found."));
            }
        }

        var groupedDetails = request.Details
            .GroupBy(detail => detail.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();
        var currentGroupedDetails = sale.Details
            .GroupBy(detail => detail.ProductId.Value)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                UnitPrice = group.First().UnitPrice
            })
            .ToList();

        var productMap = new Dictionary<Guid, Product>();
        var saleDetails = new List<SaleDetail>();
        var stockMap = new Dictionary<Guid, BranchProductStock>();

        foreach (var detail in groupedDetails)
        {
            var product = await _productRepository.GetByIdAsync(
                new ProductId(detail.ProductId),
                _currentUserService.CompanyId,
                cancellationToken);

            if (product is null)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.NotFound("Sales.Update.ProductNotFound", $"The product '{detail.ProductId}' was not found."));
            }

            productMap[product.Id.Value] = product;
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                sale.BranchId,
                product.Id,
                _currentUserService.CompanyId,
                cancellationToken);

            stockMap[product.Id.Value] = stock;
            saleDetails.Add(SaleDetail.Create(product.Id, detail.Quantity, product.Price));
        }

        foreach (var currentDetail in currentGroupedDetails)
        {
            var stock = await _branchProductStockRepository.GetOrCreateAsync(
                sale.BranchId,
                new ProductId(currentDetail.ProductId),
                _currentUserService.CompanyId,
                cancellationToken);

            try
            {
                stock.ReleaseReservation(currentDetail.Quantity);
            }
            catch (ArgumentException ex)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.Validation("Sales.Update.InvalidQuantity", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Result<UpdateSaleResponse>.Failure(
                    Error.Conflict("Sales.Update.InvalidReservation", ex.Message));
            }

            await _stockMovementRepository.AddAsync(
                StockMovement.Create(
                    _currentUserService.CompanyId,
                    sale.BranchId,
                    stock.ProductId,
                    stock.Id,
                    StockMovementType.ReleaseReservation,
                    currentDetail.Quantity,
                    "Sale",
                    sale.Id.Value,
                    "Stock reservation released.",
                    _currentUserService.UserId),
                cancellationToken);
        }

        var requestedStatus = (SaleStatus)request.IdSaleStatus;

        if (requestedStatus == SaleStatus.Paid && !_currentUserService.HasPermission(PermissionCodes.SalesPay))
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Forbidden("Sales.Update.PaymentForbidden", "The current user does not have permission to charge sales."));
        }

        try
        {
            if (requestedStatus == SaleStatus.Paid)
            {
                if (_currentUserService.UserId is null || request.CashDrawerId is null)
                {
                    return Result<UpdateSaleResponse>.Failure(
                        Error.Validation("Sales.Update.CashDrawerRequired", "A cash drawer is required to mark the sale as paid."));
                }

                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.Reserve(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            _currentUserService.CompanyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.Reserve,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock reserved for sale.",
                            _currentUserService.UserId),
                        cancellationToken);
                }

                sale.Update(customer?.Id, SaleStatus.OnHold, request.HasDelivery, saleDetails);

                var session = await _cashSessionRepository.GetOpenForBranchAsync(
                    sale.BranchId,
                    new CashDrawerId(request.CashDrawerId.Value),
                    _currentUserService.CompanyId,
                    cancellationToken);

                if (session is null)
                {
                    return Result<UpdateSaleResponse>.Failure(
                        Error.Conflict("Sales.Update.CashSessionRequired", "An open cash session is required for the selected cash drawer."));
                }

                sale.MarkAsPaid(session.Id);
                session.RegisterSaleIncome(sale.TotalAmount, sale.Id.Value, _currentUserService.UserId);

                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.ConfirmSaleOut(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            _currentUserService.CompanyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.SaleOut,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock confirmed as sold.",
                            _currentUserService.UserId),
                        cancellationToken);
                }
            }
            else if (requestedStatus == SaleStatus.Cancel)
            {
                var currentDetailsSnapshot = currentGroupedDetails.Select(detail =>
                    SaleDetail.Create(new ProductId(detail.ProductId), detail.Quantity, detail.UnitPrice)).ToList();

                sale.Update(customer?.Id, requestedStatus, request.HasDelivery, currentDetailsSnapshot);
            }
            else
            {
                foreach (var detail in groupedDetails)
                {
                    var stock = stockMap[detail.ProductId];
                    stock.Reserve(detail.Quantity);
                    await _stockMovementRepository.AddAsync(
                        StockMovement.Create(
                            _currentUserService.CompanyId,
                            sale.BranchId,
                            stock.ProductId,
                            stock.Id,
                            StockMovementType.Reserve,
                            detail.Quantity,
                            "Sale",
                            sale.Id.Value,
                            "Stock reserved for sale.",
                            _currentUserService.UserId),
                        cancellationToken);
                }

                sale.Update(customer?.Id, requestedStatus, request.HasDelivery, saleDetails);
            }

            if (requestedStatus == SaleStatus.Cancel && existingTransportAssignmentId is not null)
            {
                var assignment = await _saleTransportAssignmentRepository.GetByIdAsync(
                    existingTransportAssignmentId,
                    _currentUserService.CompanyId,
                    cancellationToken);

                if (assignment is not null && assignment.Status != SaleTransportStatus.Delivered)
                {
                    assignment.Cancel();
                }
            }
        }
        catch (ArgumentException ex)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Validation("Sales.Update.InvalidInput", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result<UpdateSaleResponse>.Failure(
                Error.Conflict("Sales.Update.NotEditable", ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UpdateSaleResponse>.Success(
            new UpdateSaleResponse(
                sale.Id.Value,
                sale.BranchId.Value,
                sale.CustomerId?.Value,
                customer?.FullName,
                customer is null ? null : BuildCustomerDocument(customer),
                customer?.TaxId,
                sale.CashSessionId?.Value,
                sale.HasDelivery,
                sale.TransportAssignmentId?.Value,
                (int)sale.SaleStatus,
                sale.SaleStatus.ToString(),
                sale.TotalAmount,
                sale.CreatedAt,
                sale.PaidAt,
                sale.UpdatedAt,
                sale.IsModified,
                sale.Details.Select(detail => new UpdateSaleDetailItemResponse(
                    detail.ProductId.Value,
                    productMap[detail.ProductId.Value].Name,
                    productMap[detail.ProductId.Value].Brand,
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.TotalAmount)).ToList()));
    }

    private static string? BuildCustomerDocument(Customer customer)
    {
        return customer.DocumentType is null || string.IsNullOrWhiteSpace(customer.DocumentNumber)
            ? null
            : $"{customer.DocumentType} {customer.DocumentNumber}";
    }
}
