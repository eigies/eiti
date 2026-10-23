using eiti.Application.Abstractions.Data;
using eiti.Application.Abstractions.Repositories;
using eiti.Application.Abstractions.Services;
using eiti.Application.Common;
using eiti.Application.Features.Suppliers.Common;
using eiti.Domain.Cash;
using eiti.Domain.Purchases;
using MediatR;

namespace eiti.Application.Features.Suppliers.Commands.CancelSupplierPaymentOrder;

// Anula una orden de pago entera: cada medio se revierte como un pago suelto (las compras vuelven a
// pendiente, los cheques a cartera, el efectivo a la caja) y la orden queda anulada. Todo o nada.
public sealed class CancelSupplierPaymentOrderHandler : IRequestHandler<CancelSupplierPaymentOrderCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierPaymentOrderRepository _supplierPaymentOrderRepository;
    private readonly ISupplierPaymentRepository _supplierPaymentRepository;
    private readonly ICashDrawerRepository _cashDrawerRepository;
    private readonly ICashSessionRepository _cashSessionRepository;
    private readonly ISupplierPaymentOrderService _supplierPaymentOrderService;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSupplierPaymentOrderHandler(
        ICurrentUserService currentUserService,
        ISupplierRepository supplierRepository,
        ISupplierPaymentOrderRepository supplierPaymentOrderRepository,
        ISupplierPaymentRepository supplierPaymentRepository,
        ICashDrawerRepository cashDrawerRepository,
        ICashSessionRepository cashSessionRepository,
        ISupplierPaymentOrderService supplierPaymentOrderService,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _supplierRepository = supplierRepository;
        _supplierPaymentOrderRepository = supplierPaymentOrderRepository;
        _supplierPaymentRepository = supplierPaymentRepository;
        _cashDrawerRepository = cashDrawerRepository;
        _cashSessionRepository = cashSessionRepository;
        _supplierPaymentOrderService = supplierPaymentOrderService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelSupplierPaymentOrderCommand command, CancellationToken cancellationToken)
    {
        var authCheck = _currentUserService.EnsureAuthenticatedWithContext();
        if (authCheck.IsFailure)
            return Result.Failure(authCheck.Error);

        var companyId = _currentUserService.CompanyId!;
        var userId = _currentUserService.UserId!;

        var order = await _supplierPaymentOrderRepository.GetByIdAsync(command.PaymentOrderId, companyId.Value, cancellationToken);
        if (order is null || order.SupplierId != command.SupplierId)
            return Result.Failure(CancelSupplierPaymentOrderErrors.NotFound);

        if (order.Status == PurchasePaymentStatus.Cancelled)
            return Result.Failure(CancelSupplierPaymentOrderErrors.AlreadyCancelled);

        var supplier = await _supplierRepository.GetByIdAsync(order.SupplierId, companyId.Value, cancellationToken);
        if (supplier is null)
            return Result.Failure(CancelSupplierPaymentOrderErrors.NotFound);

        var payments = await _supplierPaymentRepository.ListByOrderAsync(order.Id, companyId.Value, cancellationToken);

        // Si algún medio fue en efectivo hay que reintegrarlo a la caja; el resto no movió efectivo.
        // Exige caja propia (drawer asignado) o permiso CashDrawerViewAll.
        CashSession? session = null;
        if (payments.Any(p => p.Status == PurchasePaymentStatus.Active && p.Method == PurchasePaymentMethod.Cash))
        {
            var resolve = await CashSessionResolver.ResolveOpenSessionAsync(
                _currentUserService, _cashDrawerRepository, _cashSessionRepository, userId, companyId, cancellationToken);
            if (resolve.Status == CashSessionResolveStatus.NoAssignedDrawer)
                return Result.Failure(CancelSupplierPaymentOrderErrors.NoAssignedCashDrawer);
            if (resolve.Status == CashSessionResolveStatus.NoSessionOpen)
                return Result.Failure(CancelSupplierPaymentOrderErrors.NoCashSessionOpen);
            session = resolve.Session;
        }

        await _supplierPaymentOrderService.CancelOrderAsync(
            order, payments, supplier, session, companyId, userId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
