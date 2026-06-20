using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Customers.Common;
using eiti.Domain.Customers;
using eiti.Domain.Sales;
using eiti.Domain.Stock;
using MediatR;

namespace eiti.Application.Features.Customers.Commands.ApplyCustomerCredit;

public sealed class ApplyCustomerCreditHandler
    : IRequestHandler<ApplyCustomerCreditCommand, Result<ApplyCustomerCreditResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IBranchProductStockRepository _branchProductStockRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyCustomerCreditHandler(
        ICurrentUserService currentUserService,
        ICustomerRepository customerRepository,
        ISaleRepository saleRepository,
        IBranchProductStockRepository branchProductStockRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _customerRepository = customerRepository;
        _saleRepository = saleRepository;
        _branchProductStockRepository = branchProductStockRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ApplyCustomerCreditResponse>> Handle(ApplyCustomerCreditCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result<ApplyCustomerCreditResponse>.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var customer = await _customerRepository.GetByIdAsync(new CustomerId(command.CustomerId), companyId, cancellationToken);
        if (customer is null)
            return Result<ApplyCustomerCreditResponse>.Failure(ApplyCustomerCreditErrors.CustomerNotFound);

        if (customer.CreditBalance <= 0)
            return Result<ApplyCustomerCreditResponse>.Failure(ApplyCustomerCreditErrors.NoCredit);

        var creditBefore = customer.CreditBalance;

        // Imputación FIFO del saldo a favor existente a las ventas CC pendientes (sin cobro nuevo ni caja).
        var application = await CustomerCreditApplicator.ApplyToPendingCcSalesAsync(
            customer, companyId, _saleRepository, cancellationToken, customerPaymentId: null);
        _customerRepository.Update(customer);

        // Confirmar stock de las ventas que pasaron a Paid con estas imputaciones.
        // Reusa las entidades tracked del applicator (ya traen Details) — sin re-fetch por venta.
        foreach (var sale in application.SalesNowPaidEntities)
        {
            foreach (var detail in sale.Details)
            {
                var stock = await _branchProductStockRepository.GetOrCreateAsync(
                    sale.BranchId,
                    detail.ProductId,
                    companyId,
                    cancellationToken);

                stock.ConfirmSaleOut(detail.Quantity);
                await _stockMovementRepository.AddAsync(
                    StockMovement.Create(
                        companyId,
                        sale.BranchId,
                        stock.ProductId,
                        stock.Id,
                        StockMovementType.SaleOut,
                        detail.Quantity,
                        "Sale",
                        sale.Id.Value,
                        "Stock confirmed as sold (CC paid via existing credit reconciliation).",
                        userId),
                    cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var applied = creditBefore - customer.CreditBalance;

        return Result<ApplyCustomerCreditResponse>.Success(new ApplyCustomerCreditResponse(
            customer.Id.Value,
            applied,
            customer.CreditBalance,
            application.Imputaciones));
    }
}
